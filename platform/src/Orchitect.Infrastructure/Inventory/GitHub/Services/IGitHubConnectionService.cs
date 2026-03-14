using Octokit;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public interface IGitHubConnectionService
{
    GitHubClient Client { get; }
}