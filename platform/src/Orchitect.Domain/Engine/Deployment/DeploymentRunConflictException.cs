namespace Orchitect.Domain.Engine.Deployment;

public sealed class DeploymentRunConflictException(DeploymentRunId runId, Exception? innerException = null)
    : Exception($"Run {runId.Value} was changed by someone else since it was read.", innerException)
{
    public DeploymentRunId RunId { get; } = runId;
}
