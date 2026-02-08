using Microsoft.Extensions.Logging;
using Orchitect.Engine.Infrastructure.Terraform.Models;

namespace Orchitect.Engine.Infrastructure.Terraform;

public interface ITerraformProjectBuilder
{
    Task<TerraformProjectBuilderResult> BuildProject(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> validatedPlans,
        string projectFolderName);
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

    public async Task<TerraformProjectBuilderResult> BuildProject(
        Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> validatedPlans, string projectFolderName)
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "orchitect", "terraform", "state", projectFolderName);
        var plansDirectory = Path.Combine(stateDirectory, "plans");
        Directory.CreateDirectory(stateDirectory);
        Directory.CreateDirectory(plansDirectory);

        var terraformValidationResults = validatedPlans.Values.ToList();

        var mainTf = _renderer.RenderMainTf(validatedPlans);
        _logger.LogDebug("Render output: {Output}", mainTf);
        var mainTfOutputPath = Path.Combine(stateDirectory, "main.tf");
        await File.WriteAllTextAsync(mainTfOutputPath, mainTf);
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
            throw new Exception("No provider found for any templates passed.");
        }

        var providersTf = _renderer.RenderProvidersTf(providers);
        _logger.LogDebug("Render output: {Output}", providersTf);
        var providersTfOutputPath = Path.Combine(stateDirectory, "providers.tf");
        await File.WriteAllTextAsync(providersTfOutputPath, providersTf);
        _logger.LogInformation("Created providers.tf to: {FilePath}", providersTfOutputPath);

        return new TerraformProjectBuilderResult(stateDirectory, plansDirectory);
    }
}