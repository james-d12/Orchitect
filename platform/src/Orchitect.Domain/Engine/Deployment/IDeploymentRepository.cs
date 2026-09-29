using Orchitect.Domain.Core;
using Orchitect.Domain.Engine.Environment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Engine.Deployment;

public interface IDeploymentRepository : IRepository<Deployment, DeploymentId>
{
    Task<Deployment?> UpdateAsync(Deployment deployment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recently created deployment of an application to an environment.
    /// </summary>
    Task<Deployment?> GetLatestAsync(ApplicationId applicationId, EnvironmentId environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the deployments that are pending or running and were last updated before a point in time.
    /// </summary>
    Task<IReadOnlyList<Deployment>> GetActiveAsync(DateTime updatedBefore,
        CancellationToken cancellationToken = default);
}
