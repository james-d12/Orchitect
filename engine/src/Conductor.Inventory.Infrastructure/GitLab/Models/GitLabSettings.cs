using Conductor.Inventory.Infrastructure.Shared;

namespace Conductor.Inventory.Infrastructure.GitLab.Models;

public sealed class GitLabSettings : Settings
{
    public string HostUrl { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
}