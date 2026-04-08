using Orchitect.Domain.Inventory.SourceControl.Requests;

namespace Orchitect.Domain.Inventory.SourceControl.Services;

public interface ISourceControlQueryService
{
    List<Repository> QueryRepositories(RepositoryQueryRequest request);
    List<PullRequest> QueryPullRequests(PullRequestQueryRequest request);
}