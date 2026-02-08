using Conductor.Inventory.Infrastructure.GitHub.Models;
using Microsoft.Extensions.Options;
using Octokit;

namespace Conductor.Inventory.Infrastructure.GitHub.Services;

public sealed class GitHubConnectionService(IOptions<GitHubSettings> options) : IGitHubConnectionService
{
    public GitHubClient Client { get; } = new(new ProductHeaderValue(options.Value.AgentName))
    {
        Credentials = new Credentials(options.Value.Token)
    };
}