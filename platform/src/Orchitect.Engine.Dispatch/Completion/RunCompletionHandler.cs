using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Engine.Dispatch.Completion;

public interface IRunCompletionHandler
{
    /// <summary>
    /// Completes a run: records its result, finishes its deployment and revokes its token, then settles its planned
    /// instances. Returns false when the run had already completed, in which case only a missing exit code is
    /// recorded.
    /// </summary>
    Task<bool> CompleteAsync(DeploymentRunId runId, RunResult result, CancellationToken cancellationToken);
}

public sealed class RunCompletionHandler : IRunCompletionHandler
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeploymentRunRepository _runs;
    private readonly IDeploymentRepository _deployments;
    private readonly IRunCompleter _completer;
    private readonly ILogger<RunCompletionHandler> _logger;

    public RunCompletionHandler(
        IUnitOfWork unitOfWork,
        IDeploymentRunRepository runs,
        IDeploymentRepository deployments,
        IRunCompleter completer,
        ILogger<RunCompletionHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _runs = runs;
        _deployments = deployments;
        _completer = completer;
        _logger = logger;
    }

    public async Task<bool> CompleteAsync(DeploymentRunId runId, RunResult result,
        CancellationToken cancellationToken)
    {
        FinishedRun finished;

        try
        {
            finished = await _unitOfWork.ExecuteAsync(token => FinishAsync(runId, result, token),
                cancellationToken);
        }
        catch (DeploymentRunConflictException)
        {
            finished = await _unitOfWork.ExecuteAsync(token => FinishAsync(runId, result, token),
                cancellationToken);
        }

        if (finished.Settle)
        {
            await _completer.CompleteAsync(runId,
                finished.Run.Status == DeploymentRunStatus.Succeeded ? RunOutcome.Succeeded : RunOutcome.Failed,
                cancellationToken);
        }

        return finished.Completed;
    }

    private async Task<FinishedRun> FinishAsync(DeploymentRunId runId, RunResult result,
        CancellationToken cancellationToken)
    {
        await _runs.LockAsync(runId, cancellationToken);

        var run = await _runs.GetByIdAsync(runId, cancellationToken)
                  ?? throw new InvalidOperationException($"Run '{runId.Value}' was not found.");
        var deployment = await _deployments.GetByIdAsync(run.DeploymentId, cancellationToken);
        var latest = deployment is null ? null : await _runs.GetLatestAsync(deployment.Id, cancellationToken);
        var isLatest = latest?.Id == run.Id;

        if (run.IsActive)
        {
            var completed = result.Outcome == RunOutcome.Succeeded
                ? run.Succeed(result.ExitCode, result.RunnerId)
                : run.Fail(string.IsNullOrWhiteSpace(result.ErrorSummary)
                    ? RunResult.ReportedFailureSummary
                    : result.ErrorSummary, result.ExitCode, result.RunnerId);
            run = await _runs.UpdateAsync(completed, cancellationToken) ?? completed;

            if (isLatest)
            {
                await FinishDeploymentAsync(run, deployment!, cancellationToken);
            }

            _logger.LogInformation("Run {RunId} of deployment {DeploymentId} is {Status}.", run.Id.Value,
                run.DeploymentId.Value, run.Status);
            return new FinishedRun(run, true, true);
        }

        if (result.ExitCode is { } exitCode)
        {
            var recorded = run.RecordExit(exitCode, result.RunnerId);

            if (recorded != run)
            {
                run = await _runs.UpdateAsync(recorded, cancellationToken) ?? recorded;
            }
        }

        if (isLatest)
        {
            await FinishDeploymentAsync(run, deployment!, cancellationToken);
        }

        return new FinishedRun(run, false, isLatest && run.Status != DeploymentRunStatus.Cancelled);
    }

    private async Task FinishDeploymentAsync(DeploymentRun run, Deployment deployment,
        CancellationToken cancellationToken)
    {
        var owned = run.Operation == DeploymentRunOperation.Destroy
            ? deployment.Status == DeploymentStatus.Destroying
            : deployment.Status is DeploymentStatus.Pending or DeploymentStatus.Deploying;

        if (!owned || run.Status == DeploymentRunStatus.Cancelled)
        {
            return;
        }

        var finished = run.Status == DeploymentRunStatus.Succeeded
            ? deployment.Succeed()
            : deployment.Fail(run.ErrorSummary ?? RunResult.ReportedFailureSummary);

        await _deployments.UpdateAsync(finished, cancellationToken);
    }

    private sealed record FinishedRun(DeploymentRun Run, bool Completed, bool Settle);
}
