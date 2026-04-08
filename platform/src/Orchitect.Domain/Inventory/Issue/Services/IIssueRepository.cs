using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Issue.Services;

public interface IIssueRepository : IRepository<Issue, IssueId>
{
    Task<IReadOnlyList<Issue>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Issue>> GetByPlatformAsync(
        OrganisationId organisationId,
        IssuePlatform platform,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<Issue> workItems,
        CancellationToken cancellationToken = default);
}
