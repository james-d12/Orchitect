using Microsoft.Extensions.Options;
using NGitLab;
using Orchitect.Inventory.Infrastructure.GitLab.Models;

namespace Orchitect.Inventory.Infrastructure.GitLab.Services;

public sealed class GitLabConnectionService(IOptions<GitLabSettings> options) : IGitLabConnectionService
{
    public GitLabClient Client { get; } = new(options.Value.HostUrl, options.Value.Token);
}