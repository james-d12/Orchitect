using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.GitHub.Models;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public sealed class GitHubDiscoveryService : DiscoveryService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CredentialPayloadResolver _payloadResolver;

    public GitHubDiscoveryService(
        ILogger<GitHubDiscoveryService> logger,
        IMemoryCache memoryCache,
        CredentialPayloadResolver payloadResolver) : base(logger)
    {
        _memoryCache = memoryCache;
        _payloadResolver = payloadResolver;
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

        var repositories = await gitHubService.GetRepositoriesAsync(configuration.OrganisationId);

        var pullRequests = new List<GitHubPullRequest>();
        var pipelines = new List<GitHubPipeline>();

        foreach (var repository in repositories)
        {
            var repositoryPullRequests = await gitHubService.GetPullRequestsAsync(repository);
            pullRequests.AddRange(repositoryPullRequests);

            var repositoryPipelines = await gitHubService.GetPipelinesAsync(repository);
            pipelines.AddRange(repositoryPipelines);
        }

        // Use org-specific cache keys
        var orgId = configuration.OrganisationId.Value;
        _memoryCache.Set($"GitHub:Repositories:{orgId}", repositories);
        _memoryCache.Set($"GitHub:Pipelines:{orgId}", pipelines);
        _memoryCache.Set($"GitHub:PullRequests:{orgId}", pullRequests);
    }
}