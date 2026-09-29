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
}
