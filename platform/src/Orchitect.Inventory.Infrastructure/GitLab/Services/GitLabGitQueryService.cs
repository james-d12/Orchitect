using Orchitect.Inventory.Domain.Git;
using Orchitect.Inventory.Domain.Git.Request;
using Orchitect.Inventory.Domain.Git.Service;
using Orchitect.Inventory.Infrastructure.Shared.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Inventory.Infrastructure.GitLab.Constants;
using Orchitect.Inventory.Infrastructure.GitLab.Models;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Inventory.Infrastructure.Shared.Query;

namespace Orchitect.Inventory.Infrastructure.GitLab.Services;

public sealed class GitLabGitQueryService : IGitQueryService
{
    private readonly ILogger<GitLabGitQueryService> _logger;
    private readonly IMemoryCache _memoryCache;

    public GitLabGitQueryService(
        ILogger<GitLabGitQueryService> logger,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
    }

    public List<Pipeline> QueryPipelines(PipelineQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying pipelines from GitLab");
        var gitLabPipelines = _memoryCache.Get<List<GitLabPipeline>>(GitLabCacheConstants.PipelineCacheKey) ?? [];
        var pipelines = gitLabPipelines.ConvertAll<Pipeline>(p => p);

        return new QueryBuilder<Pipeline>(pipelines)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .Where(request.Name, p => p.Name.ContainsCaseInsensitive(request.Name))
            .Where(request.Url, p => p.Url.ToString().ContainsCaseInsensitive(request.Url))
            .Where(request.OwnerName, p => p.Owner.Name.EqualsCaseInsensitive(request.OwnerName))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .ToList();
    }

    public List<Repository> QueryRepositories(RepositoryQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying repositories from GitLab");
        var gitLabRepositories =
            _memoryCache.Get<List<GitLabRepository>>(GitLabCacheConstants.RepositoryCacheKey) ?? [];
        var repositories = gitLabRepositories.ConvertAll<Repository>(p => p);

        return new QueryBuilder<Repository>(repositories)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .Where(request.Name, p => p.Name.ContainsCaseInsensitive(request.Name))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .Where(request.Url, p => p.Url.ToString().ContainsCaseInsensitive(request.Url))
            .Where(request.OwnerName, p => p.Owner.Name.EqualsCaseInsensitive(request.OwnerName))
            .Where(request.DefaultBranch, p => p.DefaultBranch.EqualsCaseInsensitive(request.DefaultBranch))
            .ToList();
    }

    public List<PullRequest> QueryPullRequests(PullRequestQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying pull requests from GitLab");
        var gitLabPullRequests =
            _memoryCache.Get<List<GitLabPullRequest>>(GitLabCacheConstants.PullRequestCacheKey) ?? [];
        var pullRequests = gitLabPullRequests.ConvertAll<PullRequest>(p => p);

        return new QueryBuilder<PullRequest>(pullRequests)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .Where(request.Name, p => p.Name.ContainsCaseInsensitive(request.Name))
            .Where(request.Description, p => p.Description.ContainsCaseInsensitive(request.Description))
            .Where(request.Url, p => p.Url.ToString().ContainsCaseInsensitive(request.Url))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .ToList();
    }
}