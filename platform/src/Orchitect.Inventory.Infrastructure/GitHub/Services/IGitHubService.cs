using Orchitect.Inventory.Infrastructure.GitHub.Models;

namespace Orchitect.Inventory.Infrastructure.GitHub.Services;

public interface IGitHubService
{
    Task<List<GitHubRepository>> GetRepositoriesAsync();
    Task<List<GitHubPipeline>> GetPipelinesAsync(GitHubRepository repository);

    Task<List<GitHubPullRequest>> GetPullRequestsAsync(GitHubRepository repository);
}