using Microsoft.Extensions.Options;
using Octokit;
using Orchitect.Inventory.Infrastructure.GitHub.Models;

namespace Orchitect.Inventory.Infrastructure.GitHub.Services;

public sealed class GitHubConnectionService(IOptions<GitHubSettings> options) : IGitHubConnectionService
{
    public GitHubClient Client { get; } = new(new ProductHeaderValue(options.Value.AgentName))
    {
        Credentials = new Credentials(options.Value.Token)
    };
}