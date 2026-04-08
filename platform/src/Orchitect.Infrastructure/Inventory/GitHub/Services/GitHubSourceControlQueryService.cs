using Microsoft.Extensions.Logging;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Requests;
using Orchitect.Domain.Inventory.SourceControl.Services;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using Orchitect.Infrastructure.Inventory.Shared.Query;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public sealed class GitHubSourceControlQueryService : ISourceControlQueryService
{
    private readonly ILogger<GitHubSourceControlQueryService> _logger;
    private readonly IRepositoryRepository _repositoryRepository;
    private readonly IPullRequestRepository _pullRequestRepository;

    public GitHubSourceControlQueryService(
        ILogger<GitHubSourceControlQueryService> logger,
        IRepositoryRepository repositoryRepository,
        IPullRequestRepository pullRequestRepository)
    {
        _logger = logger;
        _repositoryRepository = repositoryRepository;
        _pullRequestRepository = pullRequestRepository;
    }

    public List<Repository> QueryRepositories(RepositoryQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying repositories from database for organisation {OrganisationId}",
            request.OrganisationId);

        var repositories = _repositoryRepository
            .GetByPlatformAsync(request.OrganisationId, RepositoryPlatform.GitHub)
            .GetAwaiter()
            .GetResult()
            .ToList();

        return new QueryBuilder<Repository>(repositories)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .Where(request.Name, p => p.Name.ContainsCaseInsensitive(request.Name))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .Where(request.Url, p => p.Url.ToString().ContainsCaseInsensitive(request.Url))
            .Where(request.OwnerName, p => p.User.Name.EqualsCaseInsensitive(request.OwnerName))
            .Where(request.DefaultBranch, p => p.DefaultBranch.EqualsCaseInsensitive(request.DefaultBranch))
            .ToList();
    }

    public List<PullRequest> QueryPullRequests(PullRequestQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying pull requests from database for organisation {OrganisationId}",
            request.OrganisationId);

        var pullRequests = _pullRequestRepository
            .GetByOrganisationIdAsync(request.OrganisationId)
            .GetAwaiter()
            .GetResult()
            .Where(pr => pr.Platform == PullRequestPlatform.GitHub)
            .ToList();

        return new QueryBuilder<PullRequest>(pullRequests)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .Where(request.Name, p => p.Name.ContainsCaseInsensitive(request.Name))
            .Where(request.Description, p => p.Description.ContainsCaseInsensitive(request.Description))
            .Where(request.Url, p => p.Url.ToString().ContainsCaseInsensitive(request.Url))
            .Where(request.Platform, p => p.Platform == request.Platform)
            .ToList();
    }
}