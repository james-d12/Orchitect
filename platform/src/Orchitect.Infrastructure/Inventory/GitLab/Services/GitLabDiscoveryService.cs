using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.GitLab.Extensions;
using Orchitect.Infrastructure.Inventory.GitLab.Models;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.GitLab.Services;

public sealed class GitLabDiscoveryService : DiscoveryService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CredentialPayloadResolver _payloadResolver;
    private readonly ILoggerFactory _loggerFactory;

    public GitLabDiscoveryService(
        ILogger<GitLabDiscoveryService> logger,
        IMemoryCache memoryCache,
        CredentialPayloadResolver payloadResolver,
        ILoggerFactory loggerFactory) : base(logger)
    {
        _memoryCache = memoryCache;
        _payloadResolver = payloadResolver;
        _loggerFactory = loggerFactory;
    }

    public override string Platform => "GitLab";

    protected override Task StartAsync(
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

        var pipelines = new List<GitLabPipeline>();
        foreach (var project in projects)
        {
            var projectPipelines = gitLabService.GetPipelines(project, configuration.OrganisationId);
            pipelines.AddRange(projectPipelines);
        }

        // Use org-specific cache keys
        var orgId = configuration.OrganisationId.Value;
        _memoryCache.Set($"GitLab:Pipelines:{orgId}", pipelines);
        _memoryCache.Set($"GitLab:PullRequests:{orgId}", pullRequests);
        _memoryCache.Set($"GitLab:Repositories:{orgId}", repositories);

        return Task.FromResult(true);
    }
}