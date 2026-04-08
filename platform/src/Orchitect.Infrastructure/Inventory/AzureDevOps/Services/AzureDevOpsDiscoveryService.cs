using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Identity.Services;
using Orchitect.Domain.Inventory.Issue;
using Orchitect.Domain.Inventory.Issue.Services;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.Pipeline.Services;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Services;
using Orchitect.Infrastructure.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public sealed class AzureDevOpsDiscoveryService : DiscoveryService
{
    private readonly ILogger<AzureDevOpsDiscoveryService> _logger;
    private readonly CredentialPayloadResolver _payloadResolver;
    private readonly IRepositoryRepository _repositoryRepository;
    private readonly IPipelineRepository _pipelineRepository;
    private readonly IPullRequestRepository _pullRequestRepository;
    private readonly IIssueRepository _issueRepository;
    private readonly ITeamRepository _teamRepository;

    public AzureDevOpsDiscoveryService(
        ILogger<AzureDevOpsDiscoveryService> logger,
        CredentialPayloadResolver payloadResolver,
        IRepositoryRepository repositoryRepository,
        IPipelineRepository pipelineRepository,
        IPullRequestRepository pullRequestRepository,
        IIssueRepository issueRepository,
        ITeamRepository teamRepository) : base(logger)
    {
        _logger = logger;
        _payloadResolver = payloadResolver;
        _repositoryRepository = repositoryRepository;
        _pipelineRepository = pipelineRepository;
        _pullRequestRepository = pullRequestRepository;
        _issueRepository = issueRepository;
        _teamRepository = teamRepository;
    }

    public override string Platform => "AzureDevOps";

    protected override async Task StartAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        // Create connection service from credential
        var connectionService = AzureDevOpsConnectionService.FromCredential(
            credential,
            _payloadResolver,
            configuration.PlatformConfig);

        // Create Azure DevOps service with this connection
        var azureDevOpsService = new AzureDevOpsService(connectionService);

        // Get organization from platform config
        var organization = configuration.PlatformConfig.GetValueOrDefault("organization") ?? string.Empty;
        var projectFilters = configuration.PlatformConfig.GetValueOrDefault("projectFilters", string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        _logger.LogInformation("Discovering Azure DevOps team resources...");
        var teams = await azureDevOpsService.GetTeamsAsync(cancellationToken);

        _logger.LogInformation("Discovering Azure DevOps project resources...");
        var projects = await azureDevOpsService.GetProjectsAsync(organization, projectFilters, cancellationToken);

        var pipelines = new List<Pipeline>();
        var repositories = new List<Repository>();
        var pullRequests = new List<PullRequest>();
        var workItems = new List<Issue>();

        foreach (var project in projects)
        {
            _logger.LogInformation("Discovering Azure DevOps Repository resources for {ProjectName}", project.Name);
            var projectRepositories =
                await azureDevOpsService.GetRepositoriesAsync(project.Id, configuration.OrganisationId, cancellationToken);
            repositories.AddRange(projectRepositories);

            _logger.LogInformation("Discovering Azure DevOps Pipeline resources for {ProjectName}", project.Name);
            var projectPipelines =
                await azureDevOpsService.GetPipelinesAsync(project.Id, project.Url, configuration.OrganisationId, cancellationToken);
            pipelines.AddRange(projectPipelines);

            _logger.LogInformation("Discovering Azure DevOps Pull Request resources for {ProjectName}", project.Name);
            var projectPullRequests =
                await azureDevOpsService.GetPullRequestsAsync(project.Id, project.Url, configuration.OrganisationId, cancellationToken);
            pullRequests.AddRange(projectPullRequests);

            _logger.LogInformation("Discovering Azure DevOps Work Item resources for {ProjectName}", project.Name);
            var projectWorkItems =
                await azureDevOpsService.GetWorkItemsAsync(project.Name, project.Url, configuration.OrganisationId, cancellationToken);
            workItems.AddRange(projectWorkItems);
        }

        // Persist all discovered data to database
        await _teamRepository.BulkUpsertAsync(teams, cancellationToken);
        await _repositoryRepository.BulkUpsertAsync(repositories, cancellationToken);
        await _pipelineRepository.BulkUpsertAsync(pipelines, cancellationToken);
        await _pullRequestRepository.BulkUpsertAsync(pullRequests, cancellationToken);
        await _issueRepository.BulkUpsertAsync(workItems, cancellationToken);

        _logger.LogInformation(
            "Azure DevOps discovery completed for organisation {OrganisationId}: {TeamCount} teams, {RepositoryCount} repositories, {PipelineCount} pipelines, {PullRequestCount} pull requests, {WorkItemCount} work items",
            configuration.OrganisationId.Value,
            teams.Count,
            repositories.Count,
            pipelines.Count,
            pullRequests.Count,
            workItems.Count);
    }
}