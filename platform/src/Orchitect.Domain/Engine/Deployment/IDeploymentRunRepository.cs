using Orchitect.Domain.Core;

namespace Orchitect.Domain.Engine.Deployment;

public interface IDeploymentRunRepository : IRepository<DeploymentRun, DeploymentRunId>
{
    Task<DeploymentRun?> UpdateAsync(DeploymentRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recently queued run of a deployment.
    /// </summary>
    Task<DeploymentRun?> GetLatestAsync(DeploymentId deploymentId, CancellationToken cancellationToken = default);
}
