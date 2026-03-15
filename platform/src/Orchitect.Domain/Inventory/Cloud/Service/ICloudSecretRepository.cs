using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Cloud.Service;

public interface ICloudSecretRepository : IRepository<CloudSecret, CloudSecretId>
{
    Task<IReadOnlyList<CloudSecret>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CloudSecret>> GetByPlatformAsync(
        OrganisationId organisationId,
        CloudSecretPlatform platform,
        CancellationToken cancellationToken = default);

    Task<CloudSecret?> GetByUrlAsync(
        string url,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<CloudSecret> cloudSecrets,
        CancellationToken cancellationToken = default);
}
