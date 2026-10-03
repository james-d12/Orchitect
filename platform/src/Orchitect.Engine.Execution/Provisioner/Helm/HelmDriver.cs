using Microsoft.Extensions.Logging;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Provisioner.Helm.Models;

namespace Orchitect.Engine.Execution.Provisioner.Helm;

public interface IHelmDriver
{
    Task PlanAsync(RunInput input);
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

    public async Task PlanAsync(RunInput input)
    {
        var result = await _validator.ValidateAsync(input);

        switch (result.State)
        {
            case HelmValidationResultState.WrongProvider:
            case HelmValidationResultState.TemplateNotFound:
            case HelmValidationResultState.ModuleNotFound:
            case HelmValidationResultState.ModuleNotParsable:
            case HelmValidationResultState.InputNotPresent:
                _logger.LogError("Helm Validation for {Template} Failed due to: {State} with Message: {Message}",
                    input.TemplateName, result.State,
                    result.Message);
                break;
            case HelmValidationResultState.Valid:
                _logger.LogInformation("Helm Validation for {Template} Passed.", input.TemplateName);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}