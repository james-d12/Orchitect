using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Artifact;
using Orchitect.Engine.Execution.Provisioner.Terraform.Models;
using Orchitect.Engine.Execution.Shared.CommandLine;

namespace Orchitect.Engine.Execution.Provisioner.Terraform;

public interface ITerraformDriver
{
    Task<TerraformPlanResult> PlanAsync(List<RunInput> terraformPlanInputs, RunContext context,
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
    private readonly IRunArtifactStore _artifactStore;

    public TerraformDriver(ILogger<TerraformDriver> logger, ITerraformValidator validator,
        ITerraformCommandLine commandLine, ITerraformProjectBuilder projectBuilder, IRunArtifactStore artifactStore)
    {
        _logger = logger;
        _validator = validator;
        _commandLine = commandLine;
        _projectBuilder = projectBuilder;
        _artifactStore = artifactStore;
    }

    public async Task<TerraformPlanResult> PlanAsync(List<RunInput> terraformPlanInputs,
        RunContext context, bool destroy = false, CancellationToken cancellationToken = default)
    {
        var validationResults = await _validator.ValidateAsync(terraformPlanInputs, cancellationToken);

        var validResults = new Dictionary<RunInput, TerraformValidationResult.ValidResult>();
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
                        result.Key.TemplateName, result.Value.State, result.Value.Message);
                    validationErrors.Add($"{result.Key.TemplateName}: {result.Value.Message}");
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
            await _commandLine.RunInitAsync(builderResult.WorkingDirectory, builderResult.BackendConfigFile,
                cancellationToken);

        await SaveLogAsync("init", initResult, cancellationToken);

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

        await SaveLogAsync("validate", validateResult, cancellationToken);

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

        await SaveLogAsync("plan", planResult, cancellationToken);

        switch (planResult.ExitCode)
        {
            case (int)TerraformPlanResultExitCode.ChangesNeeded:
                _logger.LogInformation("Successfully run plan for {ProjectName}", context.ProjectName);
                await SavePlanAsync(builderResult.WorkingDirectory, planFileName, cancellationToken);
                return new TerraformPlanResult(builderResult.WorkingDirectory, planFileName,
                    TerraformPlanResultState.Success, planResult);
            case (int)TerraformPlanResultExitCode.NoChanges:
                await SavePlanAsync(builderResult.WorkingDirectory, planFileName, cancellationToken);
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
                cancellationToken), cancellationToken);

    public Task DestroyAsync(TerraformPlanResult planResult, CancellationToken cancellationToken = default) =>
        ExecutePlanAsync(planResult, "Destroy",
            () => _commandLine.RunApplyAsync(planResult.WorkingDirectory, planResult.PlanFilePath,
                cancellationToken), cancellationToken);

    private async Task ExecutePlanAsync(TerraformPlanResult planResult, string operation,
        Func<Task<CommandLineResult>> execute, CancellationToken cancellationToken)
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

                await SaveLogAsync(operation.ToLowerInvariant(), result, cancellationToken);

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

    private Task SaveLogAsync(string command, CommandLineResult result, CancellationToken cancellationToken) =>
        _artifactStore.SaveTextAsync(RunArtifactKind.Log, $"terraform-{command}.log",
            $"exit code: {result.ExitCode}\n\n--- stdout ---\n{result.StdOut}\n--- stderr ---\n{result.StdErr}",
            cancellationToken);

    private async Task SavePlanAsync(string workingDirectory, string planFile, CancellationToken cancellationToken)
    {
        if (!_artifactStore.IsEnabled)
        {
            return;
        }

        var planName = Path.GetFileName(planFile);
        await _artifactStore.SaveFileAsync(RunArtifactKind.Plan, planName, planFile, cancellationToken);

        try
        {
            CommandLineResult showResult =
                await _commandLine.RunShowJsonAsync(workingDirectory, planFile, cancellationToken);

            if (showResult.ExitCode != 0)
            {
                _logger.LogWarning("Terraform Show failed with exit code {ExitCode}: {Output}", showResult.ExitCode,
                    showResult.StdErr);
                return;
            }

            await _artifactStore.SaveTextAsync(RunArtifactKind.Plan, Path.ChangeExtension(planName, ".json"),
                showResult.StdOut, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Could not render the plan {PlanFile} as JSON.", planName);
        }
    }
}
