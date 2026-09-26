using Orchitect.Domain.Core;

namespace Orchitect.Domain.Engine.Deployment;

public interface IDeploymentRepository : IRepository<Deployment, DeploymentId>
{
    Task<Deployment?> UpdateAsync(Deployment deployment, CancellationToken cancellationToken = default);
}
