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
        List<TerraformPlanInput> terraformPlanInputs, CancellationToken cancellationToken = default);
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
        List<TerraformPlanInput> terraformPlanInputs, CancellationToken cancellationToken = default)
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
            .Distinct(), cancellationToken);

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
        IEnumerable<string> moduleDirectories, CancellationToken cancellationToken)
    {
        var inspections = new ConcurrentDictionary<string, ModuleInspection>();

        await Parallel.ForEachAsync(moduleDirectories,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentInspections,
                CancellationToken = cancellationToken
            },
            async (moduleDirectory, token) =>
                inspections[moduleDirectory] = await InspectModuleAsync(moduleDirectory, token));

        _logger.LogInformation("Inspected {ModuleCount} unique modules.", inspections.Count);

        return inspections;
    }

    private async Task<ModuleInspection> InspectModuleAsync(string moduleDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.EnumerateFiles(moduleDirectory, "*.tf", SearchOption.TopDirectoryOnly).Any())
        {
            return new ModuleInspection(null, $"Could not find any .tf files in template directory: {moduleDirectory}");
        }

        CommandLineResult result = await _terraformCommandLine.RunTerraformJsonOutput(moduleDirectory, cancellationToken);

        TerraformConfig? terraformConfig;

        try
        {
            terraformConfig = JsonSerializer.Deserialize<TerraformConfig>(result.StdOut);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Could not parse json output for {Module}: {StdErr}", moduleDirectory,
                result.StdErr);
            return new ModuleInspection(null,
                $"Could not parse module in {moduleDirectory} (exit code {result.ExitCode}): {result.StdErr} {result.StdOut}".TrimEnd());
        }

        if (terraformConfig is null)
        {
            return new ModuleInspection(null,
                $"Could not parse module in {moduleDirectory} (exit code {result.ExitCode}): {result.StdErr}".TrimEnd());
        }

        var errors = terraformConfig.Diagnostics
            .Where(d => d.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
            .Select(FormatDiagnostic)
            .ToList();

        if (errors.Count > 0 || result.ExitCode != 0)
        {
            _logger.LogWarning("Module {Module} has errors: {Errors}", moduleDirectory, string.Join("; ", errors));
            var details = errors.Count > 0 ? string.Join("; ", errors) : result.StdErr;
            return new ModuleInspection(null, $"Module in {moduleDirectory} is invalid: {details}".TrimEnd());
        }

        return new ModuleInspection(terraformConfig, string.Empty);
    }

    private static string FormatDiagnostic(TerraformConfig.Diagnostic diagnostic)
    {
        var position = diagnostic.Pos is { } pos ? $"{pos.Filename}:{pos.Line}: " : string.Empty;
        var detail = string.IsNullOrWhiteSpace(diagnostic.Detail) ? string.Empty : $" - {diagnostic.Detail}";
        return $"{position}{diagnostic.Summary}{detail}";
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
            .Where(i => !terraformConfig.Variables.ContainsKey(i.Key))
            .Select(i => i.Key)
            .ToList();

        if (invalidInputs.Count > 0)
        {
            var message = $"These inputs were not present in the terraform module: {string.Join(",", invalidInputs)}";
            return TerraformValidationResult.InputInvalid(message);
        }

        var requiredInputs = terraformConfig.Variables.Values.Where(v => v.Required).ToList();
        var requiredInputsNotSatisfied = requiredInputs
            .Where(variable => !inputs.ContainsKey(variable.Name))
            .ToList();

        if (requiredInputsNotSatisfied.Count > 0)
        {
            var requiredInputNames = string.Join(",", requiredInputsNotSatisfied
                .Select(r => $"{r.Name}:{r.Type}"));
            var message =
                $"These inputs are required in the terraform module, but were not provided: {requiredInputNames}";
            return TerraformValidationResult.InputInvalid(message);
        }

        var invalidValues = inputs
            .Select(input => TerraformValueConverter.TryConvert(input.Value,
                terraformConfig.Variables[input.Key].Type, out _, out var error)
                ? null
                : $"{input.Key}: {error}")
            .OfType<string>()
            .ToList();

        if (invalidValues.Count > 0)
        {
            return TerraformValidationResult.InputInvalid(
                $"These inputs have values that do not match their variable type: {string.Join("; ", invalidValues)}");
        }

        return TerraformValidationResult.Valid(terraformConfig, moduleDirectory);
    }

    private sealed record ModuleInspection(TerraformConfig? Config, string Error);
}
