using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Engine.ResourceTemplate;

public interface IResourceTemplateRepository : IRepository<ResourceTemplate, ResourceTemplateId>
{
    Task<ResourceTemplate?> GetByTypeAsync(string type, CancellationToken cancellationToken = default);

    Task<ResourceTemplate?> UpdateAsync(ResourceTemplate resourceTemplate,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(ResourceTemplateId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the resource templates that belong to any of the organisations.
    /// </summary>
    Task<IReadOnlyList<ResourceTemplate>> GetByOrganisationIdsAsync(IReadOnlyCollection<OrganisationId> organisationIds,
        CancellationToken cancellationToken = default);
}