using Orchitect.Domain.Core.Organisation;
using Orchitect.Infrastructure.Inventory.GitHub.Models;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public interface IGitHubService
{
    Task<List<GitHubRepository>> GetRepositoriesAsync(OrganisationId organisationId);
    Task<List<GitHubPipeline>> GetPipelinesAsync(GitHubRepository repository);

    Task<List<GitHubPullRequest>> GetPullRequestsAsync(GitHubRepository repository);
}