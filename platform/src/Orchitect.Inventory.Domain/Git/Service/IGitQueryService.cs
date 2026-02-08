using Orchitect.Inventory.Domain.Git.Request;

namespace Orchitect.Inventory.Domain.Git.Service;

public interface IGitQueryService
{
    List<Pipeline> QueryPipelines(PipelineQueryRequest request);
    List<Repository> QueryRepositories(RepositoryQueryRequest request);
    List<PullRequest> QueryPullRequests(PullRequestQueryRequest request);
}