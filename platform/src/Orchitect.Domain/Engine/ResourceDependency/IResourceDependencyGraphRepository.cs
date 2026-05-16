using Orchitect.Domain.Core;
using Orchitect.Domain.Engine.Environment;

namespace Orchitect.Domain.Engine.ResourceDependency;

public interface IResourceDependencyGraphRepository
    : IRepository<ResourceDependencyGraph, ResourceDependencyGraphId>
{
    Task<ResourceDependencyGraph?> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
    Task<ResourceDependencyGraph?> UpdateAsync(ResourceDependencyGraph graph, CancellationToken cancellationToken = default);
}
