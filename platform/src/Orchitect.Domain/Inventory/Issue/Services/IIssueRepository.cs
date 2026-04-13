using Orchitect.Domain.Core;
using Orchitect.Domain.Inventory.Issue.Requests;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Issue.Services;

public interface IIssueRepository :
    IRepository<Issue, IssueId>,
    IQueryRepository<Issue, IssueQuery>
{
    Task BulkUpsertAsync(
        IEnumerable<Issue> workItems,
        CancellationToken cancellationToken = default);
}