using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Git.Service;
using Orchitect.Infrastructure.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.GitLab.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.GitLab.Services;

public sealed class GitLabDiscoveryService : DiscoveryService
{
    private readonly ILogger<GitLabDiscoveryService> _logger;
    private readonly CredentialPayloadResolver _payloadResolver;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRepositoryRepository _repositoryRepository;
    private readonly IPipelineRepository _pipelineRepository;
    private readonly IPullRequestRepository _pullRequestRepository;

    public GitLabDiscoveryService(
        ILogger<GitLabDiscoveryService> logger,
        CredentialPayloadResolver payloadResolver,
        ILoggerFactory loggerFactory,
        IRepositoryRepository repositoryRepository,
        IPipelineRepository pipelineRepository,
        IPullRequestRepository pullRequestRepository) : base(logger)
    {
        _logger = logger;
        _payloadResolver = payloadResolver;
        _loggerFactory = loggerFactory;
        _repositoryRepository = repositoryRepository;
        _pipelineRepository = pipelineRepository;
        _pullRequestRepository = pullRequestRepository;
    }

    public override string Platform => "GitLab";

    protected override async Task StartAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        // Create connection service from credential
        var connectionService = GitLabConnectionService.FromCredential(
            credential,
            _payloadResolver,
            configuration.PlatformConfig);

        // Create GitLab service with this connection
        var gitLabServiceLogger = _loggerFactory.CreateLogger<GitLabService>();
        var gitLabService = new GitLabService(gitLabServiceLogger, connectionService);

        var projects = gitLabService.GetProjects();

        var repositories = projects.Select(p => p.MapToGitLabRepository(configuration.OrganisationId)).ToList();
        var pullRequests = gitLabService.GetPullRequests(configuration.OrganisationId);

        var pipelines = new List<Domain.Inventory.Git.Pipeline>();
        foreach (var project in projects)
        {
            var projectPipelines = gitLabService.GetPipelines(project, configuration.OrganisationId);
            pipelines.AddRange(projectPipelines);
        }

        // Persist all discovered data to database
        await _repositoryRepository.BulkUpsertAsync(repositories, cancellationToken);
        await _pipelineRepository.BulkUpsertAsync(pipelines, cancellationToken);
        await _pullRequestRepository.BulkUpsertAsync(pullRequests, cancellationToken);

        _logger.LogInformation(
            "GitLab discovery completed for organisation {OrganisationId}: {RepositoryCount} repositories, {PipelineCount} pipelines, {PullRequestCount} pull requests",
            configuration.OrganisationId.Value,
            repositories.Count,
            pipelines.Count,
            pullRequests.Count);
    }
}