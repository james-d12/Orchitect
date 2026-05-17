using Microsoft.Extensions.Logging;
using Orchitect.Infrastructure.Engine.Terraform.Models;

namespace Orchitect.Infrastructure.Engine.Terraform;

public interface ITerraformProjectBuilder
{
    /// <summary>
    /// Creates a Project for the Terraform main.tf and state to be saved to.
    /// </summary>
    /// <param name="validatedPlans">A list of validated plans to build against.</param>
    /// <param name="projectFolderName">The name of the folder we are wanting to create the project in.</param>
    /// <param name="cancellationToken">The cancellation token to be provided.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">The Directory provided is not in a valid state.</exception>
    /// <exception cref="InvalidOperationException">There are no providers available to build the project.</exception>
    Task<TerraformProjectBuilderResult> BuildProjectAsync(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> validatedPlans,
        string projectFolderName,
        CancellationToken cancellationToken = default);
}

public sealed class TerraformProjectBuilder : ITerraformProjectBuilder
{
    private readonly ILogger<TerraformProjectBuilder> _logger;
    private readonly ITerraformRenderer _renderer;

    public TerraformProjectBuilder(ILogger<TerraformProjectBuilder> logger, ITerraformRenderer renderer)
    {
        _logger = logger;
        _renderer = renderer;
    }

    /// <inheritdoc/>
    public async Task<TerraformProjectBuilderResult> BuildProjectAsync(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> validatedPlans,
        string projectFolderName,
        CancellationToken cancellationToken = default)
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "orchitect", "terraform", "state", projectFolderName);
        var plansDirectory = Path.Combine(stateDirectory, "plans");

        if (Directory.Exists(stateDirectory) || Directory.Exists(plansDirectory))
        {
            throw new InvalidOperationException("Cannot build project in directory that has remnant / existing files.");
        }

        var terraformValidationResults = validatedPlans.Values.ToList();

        var mainTf = _renderer.RenderMainTf(validatedPlans);
        _logger.LogDebug("Render output: {Output}", mainTf);
        var mainTfOutputPath = Path.Combine(stateDirectory, "main.tf");
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
        var providersTfOutputPath = Path.Combine(stateDirectory, "providers.tf");
        await File.WriteAllTextAsync(providersTfOutputPath, providersTf, cancellationToken);
        _logger.LogInformation("Created providers.tf to: {FilePath}", providersTfOutputPath);

        return new TerraformProjectBuilderResult(stateDirectory, plansDirectory);
    }
}