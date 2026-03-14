using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Models;
using Orchitect.Infrastructure.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public sealed class AzureDevOpsDiscoveryService : DiscoveryService
{
    private readonly ILogger<AzureDevOpsDiscoveryService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly CredentialPayloadResolver _payloadResolver;

    public AzureDevOpsDiscoveryService(
        ILogger<AzureDevOpsDiscoveryService> logger,
        IMemoryCache memoryCache,
        CredentialPayloadResolver payloadResolver) : base(logger)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _payloadResolver = payloadResolver;
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

        var pipelines = new List<AzureDevOpsPipeline>();
        var repositories = new List<AzureDevOpsRepository>();
        var pullRequests = new List<AzureDevOpsPullRequest>();
        var workItems = new List<AzureDevOpsWorkItem>();

        foreach (var project in projects)
        {
            _logger.LogInformation("Discovering Azure DevOps Repository resources for {ProjectName}", project.Name);
            var projectRepositories =
                await azureDevOpsService.GetRepositoriesAsync(project.Id, cancellationToken);
            repositories.AddRange(projectRepositories);

            _logger.LogInformation("Discovering Azure DevOps Pipeline resources for {ProjectName}", project.Name);
            var projectPipelines =
                await azureDevOpsService.GetPipelinesAsync(project.Id, project.Url, cancellationToken);
            pipelines.AddRange(projectPipelines);

            _logger.LogInformation("Discovering Azure DevOps Pull Request resources for {ProjectName}", project.Name);
            var projectPullRequests =
                await azureDevOpsService.GetPullRequestsAsync(project.Id, project.Url, cancellationToken);
            pullRequests.AddRange(projectPullRequests);

            _logger.LogInformation("Discovering Azure DevOps Work Item resources for {ProjectName}", project.Name);
            var projectWorkItems =
                await azureDevOpsService.GetWorkItemsAsync(project.Name, project.Url, cancellationToken);
            workItems.AddRange(projectWorkItems);
        }

        // Use org-specific cache keys
        var orgId = configuration.OrganisationId.Value;
        _memoryCache.Set($"AzureDevOps:Projects:{orgId}", projects);
        _memoryCache.Set($"AzureDevOps:Teams:{orgId}", teams);
        _memoryCache.Set($"AzureDevOps:Pipelines:{orgId}", pipelines);
        _memoryCache.Set($"AzureDevOps:Repositories:{orgId}", repositories);
        _memoryCache.Set($"AzureDevOps:PullRequests:{orgId}", pullRequests);
        _memoryCache.Set($"AzureDevOps:WorkItems:{orgId}", workItems);
    }
}