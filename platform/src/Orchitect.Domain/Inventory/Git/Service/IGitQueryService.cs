using Orchitect.Domain.Inventory.Git.Request;

namespace Orchitect.Domain.Inventory.Git.Service;

public interface IGitQueryService
{
    List<Pipeline> QueryPipelines(PipelineQueryRequest request);
    List<Repository> QueryRepositories(RepositoryQueryRequest request);
    List<PullRequest> QueryPullRequests(PullRequestQueryRequest request);
}