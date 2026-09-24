using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformValidator
{
    Task<Dictionary<TerraformPlanInput, TerraformValidationResult>> ValidateAsync(
        List<TerraformPlanInput> terraformPlanInputs);
}

public sealed class TerraformValidator : ITerraformValidator
{
    private const int MaxConcurrentInspections = 4;

    private readonly ILogger<TerraformValidator> _logger;
    private readonly ITerraformModuleDownloader _moduleDownloader;
    private readonly ITerraformCommandLine _terraformCommandLine;

    public TerraformValidator(ILogger<TerraformValidator> logger, ITerraformModuleDownloader moduleDownloader,
        ITerraformCommandLine terraformCommandLine)
    {
        _logger = logger;
        _moduleDownloader = moduleDownloader;
        _terraformCommandLine = terraformCommandLine;
    }

    public async Task<Dictionary<TerraformPlanInput, TerraformValidationResult>> ValidateAsync(
        List<TerraformPlanInput> terraformPlanInputs)
    {
        var results = new Dictionary<TerraformPlanInput, TerraformValidationResult>();
        var versions = new Dictionary<TerraformPlanInput, ResourceTemplateVersion>();

        foreach (var planInput in terraformPlanInputs)
        {
            _logger.LogInformation("Validating Template: {Template} using the Terraform Driver.",
                planInput.Template.Name);

            var (version, error) = ResolveVersion(planInput.Template);

            if (version is null)
            {
                results[planInput] = TerraformValidationResult.TemplateInvalid(error);
                continue;
            }

            versions[planInput] = version;
        }

        var downloads = await _moduleDownloader.DownloadAsync(versions.Values.Select(v => v.Source));

        var modules = await InspectModulesAsync(downloads.Values
            .Where(download => download.IsSuccess)
            .Select(download => download.Directory!)
            .Distinct());

        foreach (var (planInput, version) in versions)
        {
            results[planInput] = ValidateInputs(planInput, version, downloads[version.Source], modules);
        }

        return terraformPlanInputs.ToDictionary(planInput => planInput, planInput => results[planInput]);
    }

    private static (ResourceTemplateVersion? Version, string Error) ResolveVersion(ResourceTemplate template)
    {
        if (template.Provider != ResourceTemplateProvider.Terraform)
        {
            return (null, $"The template: {template.Name} is configured to use {template.Provider}");
        }

        return template.GetLatestVersion() is { } version
            ? (version, string.Empty)
            : (null, $"No Version could be found for {template.Name} found.");
    }

    private async Task<IReadOnlyDictionary<string, ModuleInspection>> InspectModulesAsync(
        IEnumerable<string> moduleDirectories)
    {
        var inspections = new ConcurrentDictionary<string, ModuleInspection>();

        await Parallel.ForEachAsync(moduleDirectories,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentInspections },
            async (moduleDirectory, _) =>
                inspections[moduleDirectory] = await InspectModuleAsync(moduleDirectory));

        _logger.LogInformation("Inspected {ModuleCount} unique modules.", inspections.Count);

        return inspections;
    }

    private async Task<ModuleInspection> InspectModuleAsync(string moduleDirectory)
    {
        var (isValidModule, errorMessage) = IsValidModuleDirectory(moduleDirectory);

        if (!isValidModule)
        {
            return new ModuleInspection(null, errorMessage);
        }

        TerraformConfig? terraformConfig = await ParseTerraformModuleAsync(moduleDirectory);

        return terraformConfig is null
            ? new ModuleInspection(null, $"Could not parse module in {moduleDirectory}")
            : new ModuleInspection(terraformConfig, string.Empty);
    }

    private static TerraformValidationResult ValidateInputs(TerraformPlanInput planInput,
        ResourceTemplateVersion version, TerraformModuleDownloadResult download,
        IReadOnlyDictionary<string, ModuleInspection> modules)
    {
        var template = planInput.Template;
        var inputs = planInput.Inputs;

        if (!download.IsSuccess)
        {
            return TerraformValidationResult.ModuleInvalid(
                $"Could not clone template: {template.Name} from {version.Source}. {download.Error}");
        }

        var moduleDirectory = download.Directory!;
        var inspection = modules[moduleDirectory];

        if (inspection.Config is not { } terraformConfig)
        {
            return TerraformValidationResult.ModuleInvalid(inspection.Error);
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

        return TerraformValidationResult.Valid(terraformConfig, moduleDirectory);
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

    private sealed record ModuleInspection(TerraformConfig? Config, string Error);
}
