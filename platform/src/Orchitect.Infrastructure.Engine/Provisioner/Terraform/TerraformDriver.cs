using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Shared.CommandLine;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public interface ITerraformDriver
{
    Task<TerraformPlanResult> PlanAsync(List<TerraformPlanInput> terraformPlanInputs, ProvisionContext context,
        bool destroy = false, CancellationToken cancellationToken = default);

    Task ApplyAsync(TerraformPlanResult planResult, CancellationToken cancellationToken = default);
    Task DestroyAsync(TerraformPlanResult planResult, CancellationToken cancellationToken = default);
}

public sealed class TerraformDriver : ITerraformDriver
{
    private readonly ILogger<TerraformDriver> _logger;
    private readonly ITerraformValidator _validator;
    private readonly ITerraformCommandLine _commandLine;
    private readonly ITerraformProjectBuilder _projectBuilder;

    public TerraformDriver(ILogger<TerraformDriver> logger, ITerraformValidator validator,
        ITerraformCommandLine commandLine, ITerraformProjectBuilder projectBuilder)
    {
        _logger = logger;
        _validator = validator;
        _commandLine = commandLine;
        _projectBuilder = projectBuilder;
    }

    public async Task<TerraformPlanResult> PlanAsync(List<TerraformPlanInput> terraformPlanInputs,
        ProvisionContext context, bool destroy = false, CancellationToken cancellationToken = default)
    {
        var validationResults = await _validator.ValidateAsync(terraformPlanInputs, cancellationToken);

        var validResults = new Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult>();
        var validationErrors = new List<string>();

        foreach (var result in validationResults)
        {
            switch (result.Value)
            {
                case TerraformValidationResult.ValidResult vr:
                    validResults.Add(result.Key, vr);
                    break;
                default:
                    _logger.LogError("Validation failed for {Template}: {State} - {Message}",
                        result.Key.Template.Name, result.Value.State, result.Value.Message);
                    validationErrors.Add($"{result.Key.Template.Name}: {result.Value.Message}");
                    break;
            }
        }

        if (validResults.Count != terraformPlanInputs.Count)
        {
            _logger.LogError(
                "Could not perform Terraform Plan, as not all provided inputs were validated successfully");
            return new TerraformPlanResult(TerraformPlanResultState.PreValidationFailed,
                $"Could not validate all inputs. {string.Join("; ", validationErrors)}");
        }

        TerraformProjectBuilderResult builderResult = await _projectBuilder.BuildProjectAsync(validResults, context,
            cancellationToken);

        CommandLineResult initResult =
            await _commandLine.RunInitAsync(builderResult.WorkingDirectory, builderResult.BackendConfig,
                cancellationToken);

        if (initResult.ExitCode != 0)
        {
            _logger.LogError("Terraform Init Failed: {ExitCode} with {Output}", initResult.ExitCode,
                initResult.StdErr);
            return new TerraformPlanResult(builderResult.WorkingDirectory, string.Empty,
                TerraformPlanResultState.InitFailed, initResult);
        }

        _logger.LogDebug("Terraform Init Output: {Output}", initResult.StdOut);

        CommandLineResult validateResult = await _commandLine.RunValidateAsync(builderResult.WorkingDirectory,
            cancellationToken);

        if (validateResult.ExitCode != 0)
        {
            _logger.LogWarning("Terraform Validate Failed: {ExitCode} with {Output}", validateResult.ExitCode,
                validateResult.StdErr);
            return new TerraformPlanResult(builderResult.WorkingDirectory, string.Empty,
                TerraformPlanResultState.ValidateFailed, validateResult);
        }

        _logger.LogDebug("Terraform Validate Output: {Output}", validateResult.StdOut);

        var dateTimeIsoString = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff");
        var planFileName = Path.Combine(builderResult.PlanDirectory, $"plan-{dateTimeIsoString}.tfplan");

        CommandLineResult planResult = destroy
            ? await _commandLine.RunPlanDestroyAsync(builderResult.WorkingDirectory, planFileName, cancellationToken)
            : await _commandLine.RunPlanAsync(builderResult.WorkingDirectory, planFileName, cancellationToken);

        switch (planResult.ExitCode)
        {
            case (int)TerraformPlanResultExitCode.ChangesNeeded:
                _logger.LogInformation("Successfully run plan for {ProjectName}", context.ProjectName);
                return new TerraformPlanResult(builderResult.WorkingDirectory, planFileName,
                    TerraformPlanResultState.Success, planResult);
            case (int)TerraformPlanResultExitCode.NoChanges:
                return new TerraformPlanResult(builderResult.WorkingDirectory, planFileName,
                    TerraformPlanResultState.NoChanges, planResult);
            case (int)TerraformPlanResultExitCode.Errored:
                return new TerraformPlanResult(builderResult.WorkingDirectory, planFileName,
                    TerraformPlanResultState.PlanFailed, planResult);
            default:
                _logger.LogError("Terraform Plan exited with unexpected code {ExitCode}", planResult.ExitCode);
                return new TerraformPlanResult(builderResult.WorkingDirectory, planFileName,
                    TerraformPlanResultState.PlanFailed, planResult);
        }
    }

    public Task ApplyAsync(TerraformPlanResult planResult, CancellationToken cancellationToken = default) =>
        ExecutePlanAsync(planResult, "Apply",
            () => _commandLine.RunApplyAsync(planResult.WorkingDirectory, planResult.PlanFilePath,
                cancellationToken));

    public Task DestroyAsync(TerraformPlanResult planResult, CancellationToken cancellationToken = default) =>
        ExecutePlanAsync(planResult, "Destroy",
            () => _commandLine.RunApplyAsync(planResult.WorkingDirectory, planResult.PlanFilePath,
                cancellationToken));

    private async Task ExecutePlanAsync(TerraformPlanResult planResult, string operation,
        Func<Task<CommandLineResult>> execute)
    {
        switch (planResult.State)
        {
            case TerraformPlanResultState.PreValidationFailed:
            case TerraformPlanResultState.InitFailed:
            case TerraformPlanResultState.ValidateFailed:
            case TerraformPlanResultState.PlanFailed:
                throw new InvalidOperationException(
                    $"Terraform {operation} was not run because the plan is in state {planResult.State}: " +
                    planResult.Message);
            case TerraformPlanResultState.NoChanges:
                _logger.LogInformation("No changes needed in this plan");
                return;
            case TerraformPlanResultState.Success:
                _logger.LogInformation("Running Terraform {Operation} in {Directory}", operation,
                    planResult.WorkingDirectory);
                CommandLineResult result = await execute();

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Terraform {operation} failed with exit code {result.ExitCode}: {result.StdErr}");
                }

                _logger.LogInformation("Terraform {Operation} Result: {Result}", operation, result.StdOut);
                return;
            default:
                throw new InvalidEnumArgumentException(nameof(planResult.State), (int)planResult.State,
                    typeof(TerraformPlanResultState));
        }
    }
}