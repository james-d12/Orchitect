using Conductor.Inventory.Domain.Git.Request;

namespace Conductor.Inventory.Domain.Git.Service;

public interface IGitQueryService
{
    List<Pipeline> QueryPipelines(PipelineQueryRequest request);
    List<Repository> QueryRepositories(RepositoryQueryRequest request);
    List<PullRequest> QueryPullRequests(PullRequestQueryRequest request);
}