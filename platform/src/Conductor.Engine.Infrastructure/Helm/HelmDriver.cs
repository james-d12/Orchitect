using Conductor.Engine.Domain.ResourceTemplate;
using Conductor.Engine.Infrastructure.Helm.Models;
using Microsoft.Extensions.Logging;

namespace Conductor.Engine.Infrastructure.Helm;

public interface IHelmDriver
{
    Task PlanAsync(ResourceTemplate template, Dictionary<string, string> inputs);
}

public sealed class HelmDriver : IHelmDriver
{
    private readonly ILogger<HelmDriver> _logger;
    private readonly IHelmValidator _validator;

    public HelmDriver(ILogger<HelmDriver> logger, IHelmValidator validator)
    {
        _logger = logger;
        _validator = validator;
    }

    public async Task PlanAsync(ResourceTemplate template, Dictionary<string, string> inputs)
    {
        var result = await _validator.ValidateAsync(template, inputs);

        switch (result.State)
        {
            case HelmValidationResultState.WrongProvider:
            case HelmValidationResultState.TemplateNotFound:
            case HelmValidationResultState.ModuleNotFound:
            case HelmValidationResultState.ModuleNotParsable:
            case HelmValidationResultState.InputNotPresent:
                _logger.LogError("Helm Validation for {Template} Failed due to: {State} with Message: {Message}",
                    template.Name, result.State,
                    result.Message);
                break;
            case HelmValidationResultState.Valid:
                _logger.LogInformation("Helm Validation for {Template} Passed.", template.Name);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}