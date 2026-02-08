using Orchitect.Inventory.Infrastructure.Shared;

namespace Orchitect.Inventory.Infrastructure.GitHub.Models;

public sealed class GitHubSettings : Settings
{
    public string AgentName { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
}