using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Engine.Dispatch.Queue;

public interface IDeploymentRunCancellation
{
    /// <summary>
    /// Tracks a run this API process is working on, until the returned handle is disposed.
    /// </summary>
    public DeploymentRunCancellationHandle Track(DeploymentRunId runId);

    /// <summary>
    /// Asks a tracked run to stop. Returns false when this API process is not working on the run.
    /// </summary>
    public bool RequestStop(DeploymentRunId runId);
}
