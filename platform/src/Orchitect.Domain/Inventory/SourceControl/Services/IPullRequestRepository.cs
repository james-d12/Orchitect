using Orchitect.Domain.Core;
using Orchitect.Domain.Inventory.SourceControl.Requests;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.SourceControl.Services;

public interface IPullRequestRepository :
    IRepository<PullRequest, PullRequestId>,
    IQueryRepository<PullRequest, PullRequestQuery>
{
    Task BulkUpsertAsync(
        IEnumerable<PullRequest> pullRequests,
        CancellationToken cancellationToken = default);
}
