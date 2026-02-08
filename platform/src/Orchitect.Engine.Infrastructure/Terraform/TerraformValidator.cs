using System.Text.Json;
using Orchitect.Engine.Domain.ResourceTemplate;
using Microsoft.Extensions.Logging;
using Orchitect.Engine.Infrastructure.CommandLine;
using Orchitect.Engine.Infrastructure.Terraform.Models;

namespace Orchitect.Engine.Infrastructure.Terraform;

public interface ITerraformValidator
{
    Task<Dictionary<TerraformPlanInput, TerraformValidationResult>> ValidateAsync(
        List<TerraformPlanInput> terraformPlanInputs);
}

public sealed class TerraformValidator : ITerraformValidator
{
    private readonly ILogger<TerraformValidator> _logger;
    private readonly IGitCommandLine _gitCommandLine;
    private readonly ITerraformCommandLine _terraformCommandLine;

    public TerraformValidator(ILogger<TerraformValidator> logger, IGitCommandLine gitCommandLine,
        ITerraformCommandLine terraformCommandLine)
    {
        _logger = logger;
        _gitCommandLine = gitCommandLine;
        _terraformCommandLine = terraformCommandLine;
    }

    public async Task<Dictionary<TerraformPlanInput, TerraformValidationResult>> ValidateAsync(
        List<TerraformPlanInput> terraformPlanInputs)
    {
        var validateTasks = terraformPlanInputs.Select(ValidatePlanAsync).ToList();
        var results = await Task.WhenAll(validateTasks);
        return terraformPlanInputs
            .Zip(results, (input, result) => new { input, result })
            .ToDictionary(x => x.input, x => x.result);
    }

    private async Task<TerraformValidationResult> ValidatePlanAsync(TerraformPlanInput terraformPlanInput)
    {
        ResourceTemplate template = terraformPlanInput.Template;
        var inputs = terraformPlanInput.Inputs;

        _logger.LogInformation("Validating Template: {Template} using the Terraform Driver.", template.Name);

        if (template.Provider != ResourceTemplateProvider.Terraform)
        {
            var message = $"The template: {template.Name} is configured to use {template.Provider}";
            return TerraformValidationResult.TemplateInvalid(message);
        }

        ResourceTemplateVersion? latestVersion = template.GetLatestVersion();
        if (latestVersion is null)
        {
            var message = $"No Version could be found for {template.Name} found.";
            return TerraformValidationResult.TemplateInvalid(message);
        }

        var basePath = Path.Combine(Path.GetTempPath(), "orchitect", "terraform");
        var templateDir = Path.Combine(basePath, "modules", template.Name.Replace(" ", "."), latestVersion.Version);
        var cloneResult = await CloneModuleAsync(latestVersion, templateDir);

        if (!cloneResult)
        {
            var message = $"Could not clone template: {template.Name} from {latestVersion.Source}";
            return TerraformValidationResult.ModuleInvalid(message);
        }

        _logger.LogInformation("Successfully cloned Repository: {Url} to {Output}", latestVersion.Source, templateDir);

        if (!string.IsNullOrEmpty(latestVersion.Source.FolderPath))
        {
            templateDir = Path.Combine(templateDir, latestVersion.Source.FolderPath);
        }

        var (isValidModule, errorMessage) = IsValidModuleDirectory(templateDir);

        if (!isValidModule)
        {
            return TerraformValidationResult.ModuleInvalid(errorMessage);
        }

        TerraformConfig? terraformConfig = await ParseTerraformModuleAsync(templateDir);

        if (terraformConfig is null)
        {
            var message = $"Could not parse module: {template.Name} from {latestVersion.Source}";
            return TerraformValidationResult.ModuleInvalid(message);
        }

        var invalidInputs = inputs
            .Where(i =>
                !terraformConfig.Variables.Keys.Any(key => key.Equals(i.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(i => i.Key)
            .ToList();

        if (invalidInputs.Count > 0)
        {
            var message = $"These inputs were not present in the terraform module: {string.Join(",", invalidInputs)}";
            return TerraformValidationResult.InputInvalid(message);
        }

        var requiredInputs = terraformConfig.Variables.Values.Where(v => v.Required).ToList();
        var requiredInputsNotSatisfied = requiredInputs
            .Where(variable =>
                !inputs.Any(input => input.Key.Equals(variable.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (requiredInputsNotSatisfied.Count > 0)
        {
            var requiredInputNames = string.Join(",", requiredInputsNotSatisfied
                .Select(r => $"{r.Name}:{r.Type}"));
            var message =
                $"These inputs are required in the terraform module, but were not provided: {requiredInputNames}";
            return TerraformValidationResult.InputInvalid(message);
        }

        return TerraformValidationResult.Valid(terraformConfig, templateDir);
    }

    private async Task<TerraformConfig?> ParseTerraformModuleAsync(string moduleDirectory)
    {
        CommandLineResult runTerraformJsonOutput = await _terraformCommandLine.RunTerraformJsonOutput(moduleDirectory);

        if (runTerraformJsonOutput.ExitCode != 0)
        {
            _logger.LogWarning("Could not get json output for {Module}", moduleDirectory);
            return null;
        }

        return JsonSerializer.Deserialize<TerraformConfig>(runTerraformJsonOutput.StdOut);
    }

    private static (bool, string) IsValidModuleDirectory(string moduleDirectory)
    {
        var variablesFile = Directory
            .GetFiles(moduleDirectory, "variables.tf", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (variablesFile is null)
        {
            return (false, $"Could not find variables.tf in template directory: {moduleDirectory} found.");
        }

        var outputsFile = Directory
            .GetFiles(moduleDirectory, "outputs.tf", SearchOption.AllDirectories)
            .FirstOrDefault();

        return outputsFile is null
            ? (false, $"Could not find outputs.tf in template directory: {moduleDirectory} found.")
            : (true, string.Empty);
    }

    private async Task<bool> CloneModuleAsync(ResourceTemplateVersion latestVersion, string templateDir)
    {
        return !string.IsNullOrEmpty(latestVersion.Source.Tag)
            ? await _gitCommandLine.CloneTagAsync(latestVersion.Source.BaseUrl, latestVersion.Source.Tag, templateDir)
            : await _gitCommandLine.CloneAsync(latestVersion.Source.BaseUrl, templateDir);
    }
}