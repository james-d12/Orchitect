using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Inventory.Infrastructure.Discovery;
using Orchitect.Inventory.Infrastructure.GitHub.Models;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.GitHub.Services;

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

        var repositories = await gitHubService.GetRepositoriesAsync();

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