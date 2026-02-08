using Conductor.Inventory.Infrastructure.GitLab.Models;
using Microsoft.Extensions.Options;
using NGitLab;

namespace Conductor.Inventory.Infrastructure.GitLab.Services;

public sealed class GitLabConnectionService(IOptions<GitLabSettings> options) : IGitLabConnectionService
{
    public GitLabClient Client { get; } = new(options.Value.HostUrl, options.Value.Token);
}