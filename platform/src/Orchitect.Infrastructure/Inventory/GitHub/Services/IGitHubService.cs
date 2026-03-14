using Orchitect.Infrastructure.Inventory.GitHub.Models;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public interface IGitHubService
{
    Task<List<GitHubRepository>> GetRepositoriesAsync();
    Task<List<GitHubPipeline>> GetPipelinesAsync(GitHubRepository repository);

    Task<List<GitHubPullRequest>> GetPullRequestsAsync(GitHubRepository repository);
}