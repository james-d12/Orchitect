using Orchitect.Domain.Core;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;

namespace Orchitect.Domain.Engine.ResourceInstance;

public interface IResourceInstanceRepository : IRepository<ResourceInstance, ResourceInstanceId>
{
    Task<ResourceInstance?> UpdateAsync(ResourceInstance instance, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ResourceInstanceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceInstance>> GetByResourceAsync(ResourceId resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceInstance>> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);
}
