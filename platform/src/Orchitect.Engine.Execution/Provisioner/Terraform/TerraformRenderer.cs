using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Provisioner.Terraform.Models;

namespace Orchitect.Engine.Execution.Provisioner.Terraform;

public interface ITerraformRenderer
{
    /// <summary>
    /// Renders the module blocks as main.tf.json and their input values as terraform.tfvars.json.
    /// Input values only appear in the tfvars file, so Terraform never evaluates them as expressions, except that
    /// ${resources.key.output} references become module output expressions, which Terraform resolves in dependency order.
    /// An input's previous keys, most recent first, become a chain of moved blocks into its module, so Terraform keeps
    /// the renamed module's state.
    /// </summary>
    TerraformRenderedModules RenderModules(
        Dictionary<RunInput, TerraformValidationResult.ValidResult> terraformValidationResults);

    /// <summary>
    /// Renders the required_providers and provider blocks as providers.tf.json.
    /// </summary>
    string RenderProviders(List<TerraformProvider> providers);

    /// <summary>
    /// Renders a partial backend block as backend.tf.json.
    /// </summary>
    string RenderBackend(string backendType);

    /// <summary>
    /// Renders backend settings as a backend.tfbackend file for terraform init -backend-config.
    /// Values are escaped so Terraform reads them as literal strings.
    /// </summary>
    string RenderBackendConfig(IReadOnlyDictionary<string, string> config);
}

public sealed partial class TerraformRenderer : ITerraformRenderer
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public TerraformRenderedModules RenderModules(
        Dictionary<RunInput, TerraformValidationResult.ValidResult> terraformValidationResults)
    {
        var variables = new JsonObject();
        var modules = new JsonObject();
        var tfVars = new JsonObject();
        var moduleNames = terraformValidationResults.Keys.ToDictionary(i => i.Key, ModuleName);

        foreach (var (planInput, validationResult) in terraformValidationResults)
        {
            var moduleName = moduleNames[planInput.Key];

            if (modules.ContainsKey(moduleName))
            {
                throw new InvalidOperationException(
                    $"More than one resource renders to the Terraform module name '{moduleName}'.");
            }

            var module = new JsonObject { ["source"] = validationResult.ModuleDirectory };

            foreach (var (inputName, rawValue) in planInput.Parameters)
            {
                var references = ScoreReference.Find(rawValue);

                if (references.Count > 0)
                {
                    module[inputName] = RenderReferences(rawValue, references, moduleNames, inputName, moduleName);
                    continue;
                }

                var variableName = $"{moduleName}__{inputName}";
                var variableType = validationResult.Config.Variables.GetValueOrDefault(inputName)?.Type;

                if (string.IsNullOrWhiteSpace(variableType))
                {
                    variableType = null;
                }

                if (!TerraformValueConverter.TryConvert(rawValue, variableType, out var value, out var error))
                {
                    throw new InvalidOperationException($"Input '{inputName}' of '{moduleName}' is invalid: {error}");
                }

                variables[variableName] = variableType is null
                    ? []
                    : new JsonObject { ["type"] = variableType };
                module[inputName] = $"${{var.{variableName}}}";
                tfVars[variableName] = value;
            }

            modules[moduleName] = module;
        }

        var mainTf = new JsonObject();

        if (variables.Count > 0)
        {
            mainTf["variable"] = variables;
        }

        mainTf["module"] = modules;

        var moved = RenderMoves(terraformValidationResults.Keys, moduleNames);

        if (moved.Count > 0)
        {
            mainTf["moved"] = moved;
        }

        return new TerraformRenderedModules(Serialize(mainTf), Serialize(tfVars));
    }

    public string RenderProviders(List<TerraformProvider> providers)
    {
        var requiredProviders = new JsonObject();
        var providerBlocks = new JsonObject();

        foreach (var provider in providers)
        {
            var requirement = new JsonObject { ["source"] = provider.Source };

            if (!string.IsNullOrWhiteSpace(provider.Version))
            {
                requirement["version"] = provider.Version;
            }

            requiredProviders[provider.Name] = requirement;
            providerBlocks[provider.Name] = new JsonObject { ["features"] = new JsonObject() };
        }

        return Serialize(new JsonObject
        {
            ["terraform"] = new JsonObject { ["required_providers"] = requiredProviders },
            ["provider"] = providerBlocks
        });
    }

    public string RenderBackend(string backendType) =>
        Serialize(new JsonObject
        {
            ["terraform"] = new JsonObject
            {
                ["backend"] = new JsonObject { [backendType] = new JsonObject() }
            }
        });

    public string RenderBackendConfig(IReadOnlyDictionary<string, string> config)
    {
        var builder = new StringBuilder();

        foreach (var (key, value) in config.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            if (!BackendConfigKey().IsMatch(key))
            {
                throw new InvalidOperationException($"Backend config key '{key}' is not a valid identifier.");
            }

            builder.Append(key).Append(" = \"").Append(EscapeHclString(value)).Append("\"\n");
        }

        return builder.ToString();
    }

    private static string EscapeHclString(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal)
        .Replace("${", "$${", StringComparison.Ordinal)
        .Replace("%{", "%%{", StringComparison.Ordinal);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$")]
    private static partial Regex BackendConfigKey();

    private static string ToIdentifier(string name)
    {
        var builder = new StringBuilder(name.Length);

        foreach (var character in name.ToLowerInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' ? character : '_');
        }

        if (builder.Length == 0 || !(char.IsAsciiLetter(builder[0]) || builder[0] == '_'))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static JsonArray RenderMoves(IEnumerable<RunInput> inputs, Dictionary<string, string> moduleNames)
    {
        var targets = moduleNames.Values.ToHashSet(StringComparer.Ordinal);
        var sources = new HashSet<string>(StringComparer.Ordinal);
        var moved = new JsonArray();

        foreach (var input in inputs)
        {
            var to = moduleNames[input.Key];

            foreach (var previousKey in input.PreviousKeys ?? [])
            {
                var from = ToIdentifier($"{input.TemplateName}_{previousKey}");

                if (targets.Contains(from))
                {
                    throw new InvalidOperationException(
                        $"Previous key '{previousKey}' of '{moduleNames[input.Key]}' renders to the Terraform module name '{from}', which is still in this run.");
                }

                if (!sources.Add(from))
                {
                    throw new InvalidOperationException(
                        $"More than one resource moves from the Terraform module name '{from}'.");
                }

                moved.Add(new JsonObject { ["from"] = $"module.{from}", ["to"] = $"module.{to}" });
                to = from;
            }
        }

        return moved;
    }

    private static string RenderReferences(string rawValue, IReadOnlyList<ScoreReference> references,
        Dictionary<string, string> moduleNames, string inputName, string moduleName)
    {
        var builder = new StringBuilder();
        var position = 0;

        foreach (var reference in references)
        {
            if (!moduleNames.TryGetValue(reference.Key, out var target))
            {
                throw new InvalidOperationException(
                    $"Input '{inputName}' of '{moduleName}' references resource '{reference.Key}', which is not in this run.");
            }

            builder.Append(EscapeTemplate(rawValue[position..reference.Index]))
                .Append("${module.").Append(target).Append('.').Append(reference.Output).Append('}');
            position = reference.Index + reference.Length;
        }

        return builder.Append(EscapeTemplate(rawValue[position..])).ToString();
    }

    private static string EscapeTemplate(string value) => value
        .Replace("${", "$${", StringComparison.Ordinal)
        .Replace("%{", "%%{", StringComparison.Ordinal);

    private static string ModuleName(RunInput input) => ToIdentifier($"{input.TemplateName}_{input.Key}");

    private static string Serialize(JsonNode node) => node.ToJsonString(SerializerOptions);
}
