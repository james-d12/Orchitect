using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Cloud.Services;

public interface ICloudResourceRepository : IRepository<CloudResource, CloudResourceId>
{
    Task<IReadOnlyList<CloudResource>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CloudResource>> GetByPlatformAsync(
        OrganisationId organisationId,
        CloudPlatform platform,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<CloudResource> cloudResources,
        CancellationToken cancellationToken = default);
}
