using Microsoft.Extensions.Logging;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Provisioner.Helm.Models;
using Orchitect.Engine.Execution.Shared.CommandLine;

namespace Orchitect.Engine.Execution.Provisioner.Helm;

public interface IHelmValidator
{
    Task<HelmValidationResult> ValidateAsync(RunInput input);
}

public sealed class HelmValidator : IHelmValidator
{
    private readonly ILogger<HelmValidator> _logger;
    private readonly IGitCommandLine _gitCommandLine;
    private readonly IHelmParser _parser;

    public HelmValidator(ILogger<HelmValidator> logger, IGitCommandLine gitCommandLine, IHelmParser parser)
    {
        _logger = logger;
        _gitCommandLine = gitCommandLine;
        _parser = parser;
    }

    public async Task<HelmValidationResult> ValidateAsync(RunInput input)
    {
        _logger.LogInformation("Validating Template: {Template} using the Helmchart Driver.", input.TemplateName);

        if (input.Provider != RunInputProvider.Helm)
        {
            var message = $"The template: {input.TemplateName} is configured to use {input.Provider}";
            return HelmValidationResult.WrongProvider(message);
        }

        var source = input.Source;
        var templateDir = Path.Combine(Path.GetTempPath(), "orchitect", "helm", input.TemplateName, source.Tag);
        var cloneResult = await _gitCommandLine.CloneAsync(source.BaseUrl, templateDir);

        if (!string.IsNullOrEmpty(source.Path))
        {
            templateDir = Path.Combine(templateDir, source.Path);
        }

        if (!cloneResult)
        {
            var message = $"Could not clone template: {input.TemplateName} from {source}";
            return HelmValidationResult.ModuleNotFound(message);
        }

        _logger.LogInformation("Successfully cloned Repository: {Url} to {Output}", source, templateDir);

        var config = await _parser.ParseHelmConfigAsync(templateDir);

        var invalidInputs = input.Parameters
            .Where(i =>
                !config.Any(helmInput => helmInput.Key.Equals(i.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(i => i.Key)
            .ToList();

        if (invalidInputs.Count > 0)
        {
            var message = $"These inputs were not present in the helm chart: {string.Join(",", invalidInputs)}";
            return HelmValidationResult.InputNotPresent(message);
        }

        return HelmValidationResult.Valid(config);
    }
}