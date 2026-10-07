using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;

namespace Orchitect.Domain.Engine.Resource;

public interface IResourceRepository : IRepository<Resource, ResourceId>
{
    Task<Resource?> UpdateAsync(Resource resource, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ResourceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Resource>> GetByEnvironmentAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the resources that belong to any of the organisations, optionally only those in an environment.
    /// </summary>
    Task<IReadOnlyList<Resource>> GetByOrganisationIdsAsync(IReadOnlyCollection<OrganisationId> organisationIds,
        EnvironmentId? environmentId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the resources with any of the ids.
    /// </summary>
    Task<IReadOnlyList<Resource>> GetByIdsAsync(IReadOnlyCollection<ResourceId> ids,
        CancellationToken cancellationToken = default);
}
