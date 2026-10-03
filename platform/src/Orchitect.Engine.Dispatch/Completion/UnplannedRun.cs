using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Engine.Dispatch.Completion;

internal static class UnplannedRun
{
    public const string Message =
        "The runner exited without planning the run. The runner image may be out of date.";

    public static async Task<Exception?> FindAsync(IDeploymentRunPlanRepository plans, DeploymentRunId runId,
        long? exitCode, CancellationToken cancellationToken) =>
        exitCode == 0 && await plans.GetByRunIdAsync(runId, cancellationToken) is null
            ? new InvalidOperationException(Message)
            : null;
}
