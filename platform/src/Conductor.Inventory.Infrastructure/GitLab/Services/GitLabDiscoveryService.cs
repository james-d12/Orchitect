using Conductor.Inventory.Infrastructure.Discovery;
using Conductor.Inventory.Infrastructure.GitLab.Constants;
using Conductor.Inventory.Infrastructure.GitLab.Extensions;
using Conductor.Inventory.Infrastructure.GitLab.Models;
using Conductor.Inventory.Infrastructure.Shared.Observability;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Conductor.Inventory.Infrastructure.GitLab.Services;

public sealed class GitLabDiscoveryService : DiscoveryService
{
    private readonly IGitLabService _gitLabService;
    private readonly IMemoryCache _memoryCache;

    public GitLabDiscoveryService(
        ILogger<GitLabDiscoveryService> logger,
        IGitLabService gitLabService,
        IMemoryCache memoryCache) : base(logger)
    {
        _gitLabService = gitLabService;
        _memoryCache = memoryCache;
    }

    public override string Platform => "GitLab";

    protected override Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        var projects = _gitLabService.GetProjects();

        var repositories = projects.Select(p => p.MapToGitLabRepository()).ToList();
        var pullRequests = _gitLabService.GetPullRequests();

        var pipelines = new List<GitLabPipeline>();
        foreach (var project in projects)
        {
            var projectPipelines = _gitLabService.GetPipelines(project);
            pipelines.AddRange(projectPipelines);
        }

        _memoryCache.Set(GitLabCacheConstants.PipelineCacheKey, pipelines);
        _memoryCache.Set(GitLabCacheConstants.PullRequestCacheKey, pullRequests);
        _memoryCache.Set(GitLabCacheConstants.RepositoryCacheKey, repositories);

        return Task.FromResult(true);
    }
}