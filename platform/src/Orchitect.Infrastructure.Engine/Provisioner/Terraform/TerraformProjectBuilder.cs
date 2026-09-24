using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformProjectBuilder
{
    /// <summary>
    /// Creates a Terraform project (main.tf, providers.tf and, when configured, backend.tf) for the given context.
    /// </summary>
    Task<TerraformProjectBuilderResult> BuildProjectAsync(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> validatedPlans,
        ProvisionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class TerraformProjectBuilder : ITerraformProjectBuilder
{
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

        var terraformValidationResults = validatedPlans.Values.ToList();

        var mainTf = _renderer.RenderMainTf(validatedPlans);
        _logger.LogDebug("Render output: {Output}", mainTf);
        var mainTfOutputPath = Path.Combine(workingDirectory, "main.tf");
        await File.WriteAllTextAsync(mainTfOutputPath, mainTf, cancellationToken);
        _logger.LogInformation("Created main.tf to: {FilePath}", mainTfOutputPath);

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

        var providersTf = _renderer.RenderProvidersTf(providers);
        _logger.LogDebug("Render output: {Output}", providersTf);
        var providersTfOutputPath = Path.Combine(workingDirectory, "providers.tf");
        await File.WriteAllTextAsync(providersTfOutputPath, providersTf, cancellationToken);
        _logger.LogInformation("Created providers.tf to: {FilePath}", providersTfOutputPath);

        var backendConfig = new Dictionary<string, string>();

        if (_backendOptions.IsRemote)
        {
            var backendTf = _renderer.RenderBackendTf(_backendOptions.Type!);
            var backendTfOutputPath = Path.Combine(workingDirectory, "backend.tf");
            await File.WriteAllTextAsync(backendTfOutputPath, backendTf, cancellationToken);
            _logger.LogInformation("Created backend.tf for {BackendType} to: {FilePath}", _backendOptions.Type,
                backendTfOutputPath);

            foreach (var (key, value) in _backendOptions.Config)
            {
                backendConfig[key] = ResolvePlaceholders(value, context);
            }
        }

        return new TerraformProjectBuilderResult(workingDirectory, plansDirectory, backendConfig);
    }

    private static string ResolvePlaceholders(string value, ProvisionContext context) => value
        .Replace("{applicationId}", context.ApplicationId, StringComparison.Ordinal)
        .Replace("{environmentId}", context.EnvironmentId, StringComparison.Ordinal)
        .Replace("{projectName}", context.ProjectName, StringComparison.Ordinal);
}