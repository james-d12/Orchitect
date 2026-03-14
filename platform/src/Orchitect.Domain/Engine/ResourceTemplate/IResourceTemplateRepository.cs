using Orchitect.Domain.Core;

namespace Orchitect.Domain.Engine.ResourceTemplate;

public interface IResourceTemplateRepository : IRepository<ResourceTemplate, ResourceTemplateId>
{
    Task<ResourceTemplate?> GetByTypeAsync(string type, CancellationToken cancellationToken = default);

    Task<ResourceTemplate?> UpdateAsync(ResourceTemplate resourceTemplate,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(ResourceTemplateId id, CancellationToken cancellationToken = default);
}