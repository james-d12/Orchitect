using Octokit;

namespace Conductor.Inventory.Infrastructure.GitHub.Services;

public interface IGitHubConnectionService
{
    GitHubClient Client { get; }
}