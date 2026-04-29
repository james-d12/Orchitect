using Orchitect.Domain.Core;
using Orchitect.Domain.Engine.Environment;

namespace Orchitect.Domain.Engine.Resource;

public interface IResourceRepository : IRepository<Resource, ResourceId>
{
    Task<Resource?> UpdateAsync(Resource resource, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ResourceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Resource>> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
}
