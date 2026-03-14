using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Git.Service;

public interface IRepositoryRepository : IRepository<Repository, RepositoryId>
{
    Task<IReadOnlyList<Repository>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Repository>> GetByPlatformAsync(
        OrganisationId organisationId,
        RepositoryPlatform platform,
        CancellationToken cancellationToken = default);

    Task<Repository?> GetByUrlAsync(
        string url,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        Repository repository,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<Repository> repositories,
        CancellationToken cancellationToken = default);
}
