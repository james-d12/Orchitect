using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.Pipeline.Services;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Services;
using Orchitect.Infrastructure.Inventory.Shared;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public sealed class GitHubDiscoveryService : DiscoveryService
{
    private readonly ILogger<GitHubDiscoveryService> _logger;
    private readonly CredentialPayloadResolver _payloadResolver;
    private readonly IRepositoryRepository _repositoryRepository;
    private readonly IPipelineRepository _pipelineRepository;
    private readonly IPullRequestRepository _pullRequestRepository;

    public GitHubDiscoveryService(
        ILogger<GitHubDiscoveryService> logger,
        CredentialPayloadResolver payloadResolver,
        IRepositoryRepository repositoryRepository,
        IPipelineRepository pipelineRepository,
        IPullRequestRepository pullRequestRepository) : base(logger)
    {
        _logger = logger;
        _payloadResolver = payloadResolver;
        _repositoryRepository = repositoryRepository;
        _pipelineRepository = pipelineRepository;
        _pullRequestRepository = pullRequestRepository;
    }

    public override string Platform => "GitHub";

    protected override async Task StartAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        // Create connection service from credential
        var connectionService = GitHubConnectionService.FromCredential(
            credential,
            _payloadResolver,
            configuration.PlatformConfig);

        // Create GitHub service with this connection
        var gitHubService = new GitHubService(connectionService);

        // Discover repositories
        var repositories = await gitHubService.GetRepositoriesAsync(configuration.OrganisationId);

        // Persist repositories to database
        await _repositoryRepository.BulkUpsertAsync(repositories, cancellationToken);

        var pullRequests = new List<PullRequest>();
        var pipelines = new List<Pipeline>();

        foreach (var repository in repositories)
        {
            var repositoryPullRequests = await gitHubService.GetPullRequestsAsync(repository);
            pullRequests.AddRange(repositoryPullRequests);

            var repositoryPipelines = await gitHubService.GetPipelinesAsync(repository);
            pipelines.AddRange(repositoryPipelines);
        }

        // Persist pipelines and pull requests to database
        await _pipelineRepository.BulkUpsertAsync(pipelines, cancellationToken);
        await _pullRequestRepository.BulkUpsertAsync(pullRequests, cancellationToken);

        _logger.LogInformation(
            "GitHub discovery completed for organisation {OrganisationId}: {RepositoryCount} repositories, {PipelineCount} pipelines, {PullRequestCount} pull requests",
            configuration.OrganisationId.Value,
            repositories.Count,
            pipelines.Count,
            pullRequests.Count);
    }
}