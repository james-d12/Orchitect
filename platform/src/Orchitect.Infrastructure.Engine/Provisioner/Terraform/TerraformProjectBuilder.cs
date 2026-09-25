using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformProjectBuilder
{
    /// <summary>
    /// Creates a Terraform project (main.tf.json, terraform.tfvars.json, providers.tf.json and, when configured,
    /// backend.tf.json) for the given context.
    /// </summary>
    Task<TerraformProjectBuilderResult> BuildProjectAsync(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> validatedPlans,
        ProvisionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class TerraformProjectBuilder : ITerraformProjectBuilder
{
    private static readonly string[] GeneratedFilePatterns = ["*.tf", "*.tf.json", "*.tfvars", "*.tfvars.json"];

    private readonly ILogger<TerraformProjectBuilder> _logger;
    private readonly ITerraformRenderer _renderer;
    private readonly TerraformBackendOptions _backendOptions;

    public TerraformProjectBuilder(ILogger<TerraformProjectBuilder> logger, ITerraformRenderer renderer,
        IOptions<TerraformBackendOptions> backendOptions)
    {
        _logger = logger;
        _renderer = renderer;
        _backendOptions = backendOptions.Value;
    }

    public async Task<TerraformProjectBuilderResult> BuildProjectAsync(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> validatedPlans,
        ProvisionContext context,
        CancellationToken cancellationToken = default)
    {
        if (_backendOptions.GetValidationError() is { } backendError)
        {
            throw new InvalidOperationException(backendError);
        }

        var workingDirectory = Path.Combine(Path.GetTempPath(), "orchitect", "terraform",
            context.ApplicationId, context.EnvironmentId);
        var plansDirectory = Path.Combine(workingDirectory, "plans");

        if (_backendOptions.IsRemote && Directory.Exists(workingDirectory))
        {
            Directory.Delete(workingDirectory, recursive: true);
        }

        Directory.CreateDirectory(plansDirectory);
        DeleteGeneratedFiles(workingDirectory);

        var terraformValidationResults = validatedPlans.Values.ToList();

        var modules = _renderer.RenderModules(validatedPlans);
        _logger.LogDebug("Render output: {Output}", modules.MainTfJson);
        await WriteFileAsync(workingDirectory, "main.tf.json", modules.MainTfJson, cancellationToken);
        await WriteFileAsync(workingDirectory, "terraform.tfvars.json", modules.TfVarsJson, cancellationToken);

        var providers = terraformValidationResults
            .SelectMany(vr => vr.Config.RequiredProviders)
            .DistinctBy(rp => rp.Key)
            .Select(rp => new TerraformProvider(
                Name: rp.Key,
                Source: rp.Value.Source,
                Version: rp.Value.VersionConstraints.FirstOrDefault() ?? string.Empty
            ))
            .ToList();

        if (providers is null || providers.Count == 0)
        {
            throw new InvalidOperationException("No provider found for any templates passed.");
        }

        var providersTf = _renderer.RenderProviders(providers);
        _logger.LogDebug("Render output: {Output}", providersTf);
        await WriteFileAsync(workingDirectory, "providers.tf.json", providersTf, cancellationToken);

        var backendConfig = new Dictionary<string, string>();

        if (_backendOptions.IsRemote)
        {
            var backendTf = _renderer.RenderBackend(_backendOptions.Type!);
            await WriteFileAsync(workingDirectory, "backend.tf.json", backendTf, cancellationToken);

            foreach (var (key, value) in _backendOptions.Config)
            {
                backendConfig[key] = ResolvePlaceholders(value, context);
            }
        }

        return new TerraformProjectBuilderResult(workingDirectory, plansDirectory, backendConfig);
    }

    private static void DeleteGeneratedFiles(string workingDirectory)
    {
        foreach (var file in GeneratedFilePatterns.SelectMany(pattern =>
                     Directory.EnumerateFiles(workingDirectory, pattern, SearchOption.TopDirectoryOnly)))
        {
            File.Delete(file);
        }
    }

    private async Task WriteFileAsync(string workingDirectory, string fileName, string contents,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(workingDirectory, fileName);
        await File.WriteAllTextAsync(path, contents, cancellationToken);
        _logger.LogInformation("Created {FileName} at: {FilePath}", fileName, path);
    }

    private static string ResolvePlaceholders(string value, ProvisionContext context) => value
        .Replace("{applicationId}", context.ApplicationId, StringComparison.Ordinal)
        .Replace("{environmentId}", context.EnvironmentId, StringComparison.Ordinal)
        .Replace("{projectName}", context.ProjectName, StringComparison.Ordinal);
}