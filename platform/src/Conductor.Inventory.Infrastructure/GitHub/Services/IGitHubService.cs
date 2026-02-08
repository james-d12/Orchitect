using Conductor.Inventory.Infrastructure.GitHub.Models;

namespace Conductor.Inventory.Infrastructure.GitHub.Services;

public interface IGitHubService
{
    Task<List<GitHubRepository>> GetRepositoriesAsync();
    Task<List<GitHubPipeline>> GetPipelinesAsync(GitHubRepository repository);

    Task<List<GitHubPullRequest>> GetPullRequestsAsync(GitHubRepository repository);
}