using Octokit;

namespace Orchitect.Inventory.Infrastructure.GitHub.Services;

public interface IGitHubConnectionService
{
    GitHubClient Client { get; }
}