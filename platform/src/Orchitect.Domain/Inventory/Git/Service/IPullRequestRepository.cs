using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Git.Service;

public interface IPullRequestRepository : IRepository<PullRequest, PullRequestId>
{
    Task<IReadOnlyList<PullRequest>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PullRequest>> GetByRepositoryAsync(
        string repositoryUrl,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PullRequest>> GetActiveAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<PullRequest> pullRequests,
        CancellationToken cancellationToken = default);
}
