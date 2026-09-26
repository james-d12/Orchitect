using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformRenderer
{
    /// <summary>
    /// Renders the module blocks as main.tf.json and their input values as terraform.tfvars.json.
    /// Input values only appear in the tfvars file, so Terraform never evaluates them as expressions.
    /// </summary>
    TerraformRenderedModules RenderModules(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> terraformValidationResults);

    /// <summary>
    /// Renders the required_providers and provider blocks as providers.tf.json.
    /// </summary>
    string RenderProviders(List<TerraformProvider> providers);

    /// <summary>
    /// Renders a partial backend block as backend.tf.json.
    /// </summary>
    string RenderBackend(string backendType);
}

public sealed class TerraformRenderer : ITerraformRenderer
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public TerraformRenderedModules RenderModules(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> terraformValidationResults)
    {
        var variables = new JsonObject();
        var modules = new JsonObject();
        var tfVars = new JsonObject();

        foreach (var (planInput, validationResult) in terraformValidationResults)
        {
            var moduleName = ToIdentifier($"{planInput.Template.Name}_{planInput.Key}");

            if (modules.ContainsKey(moduleName))
            {
                throw new InvalidOperationException(
                    $"More than one resource renders to the Terraform module name '{moduleName}'.");
            }

            var module = new JsonObject { ["source"] = validationResult.ModuleDirectory };

            foreach (var (inputName, rawValue) in planInput.Inputs)
            {
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
                    ? new JsonObject()
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

    private static string Serialize(JsonNode node) => node.ToJsonString(SerializerOptions);
}
