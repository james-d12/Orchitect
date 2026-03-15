using Microsoft.Extensions.Logging;
using Orchitect.Domain.Inventory.Git;
using Orchitect.Domain.Inventory.Git.Request;
using Orchitect.Domain.Inventory.Git.Service;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using Orchitect.Infrastructure.Inventory.Shared.Query;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public sealed class AzureDevOpsGitQueryService : IGitQueryService
{
    private readonly ILogger<AzureDevOpsGitQueryService> _logger;
    private readonly IRepositoryRepository _repositoryRepository;
    private readonly IPipelineRepository _pipelineRepository;
    private readonly IPullRequestRepository _pullRequestRepository;

    public AzureDevOpsGitQueryService(
        ILogger<AzureDevOpsGitQueryService> logger,
        IRepositoryRepository repositoryRepository,
        IPipelineRepository pipelineRepository,
        IPullRequestRepository pullRequestRepository)
    {
        _logger = logger;
        _repositoryRepository = repositoryRepository;
        _pipelineRepository = pipelineRepository;
        _pullRequestRepository = pullRequestRepository;
    }

    public List<Pipeline> QueryPipelines(PipelineQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying pipelines from database for organisation {OrganisationId}", request.OrganisationId);

        var pipelines = _pipelineRepository
            .GetByPlatformAsync(request.OrganisationId, PipelinePlatform.AzureDevOps)
            .GetAwaiter()
            .GetResult()
            .ToList();

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
        _logger.LogInformation("Querying repositories from database for organisation {OrganisationId}", request.OrganisationId);

        var repositories = _repositoryRepository
            .GetByPlatformAsync(request.OrganisationId, RepositoryPlatform.AzureDevOps)
            .GetAwaiter()
            .GetResult()
            .ToList();

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
        _logger.LogInformation("Querying pull requests from database for organisation {OrganisationId}", request.OrganisationId);

        var pullRequests = _pullRequestRepository
            .GetByOrganisationIdAsync(request.OrganisationId)
            .GetAwaiter()
            .GetResult()
            .Where(pr => pr.Platform == PullRequestPlatform.AzureDevOps)
            .ToList();

        return new QueryBuilder<PullRequest>(pullRequests)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .Where(request.Name, p => p.Name.ContainsCaseInsensitive(request.Name))
            .Where(request.Description, p => p.Description.ContainsCaseInsensitive(request.Description))
            .Where(request.Url, p => p.Url.ToString().ContainsCaseInsensitive(request.Url))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .Where(request.Labels, p => p.Labels.Select(l => l).Intersect(request.Labels ?? []).Any())
            .ToList();
    }
}