using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Inventory.Infrastructure.Azure.Models;
using Orchitect.Inventory.Infrastructure.Discovery;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.Azure.Services;

public sealed class AzureDiscoveryService : DiscoveryService
{
    private readonly ILogger<AzureDiscoveryService> _logger;
    private readonly IAzureService _azureService;
    private readonly IMemoryCache _memoryCache;

    public AzureDiscoveryService(
        ILogger<AzureDiscoveryService> logger,
        IAzureService azureService,
        IMemoryCache memoryCache) : base(logger)
    {
        _logger = logger;
        _azureService = azureService;
        _memoryCache = memoryCache;
    }

    public override string Platform => "Azure";

    protected override async Task StartAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Discovering Azure Tenant resources...");
        var tenants = await _azureService.GetTenantsAsync(cancellationToken);

        _logger.LogInformation("Discovering Azure Subscription resources.");

        // Parse subscription filters from platform config (comma-separated)
        var subscriptionFilters = configuration.PlatformConfig.TryGetValue("subscriptionFilters", out var filters)
            ? filters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : new List<string>();

        var subscriptions =
            await _azureService.GetSubscriptionsAsync(subscriptionFilters, cancellationToken);

        var cloudResources = new List<AzureCloudResource>();

        foreach (var subscription in subscriptions)
        {
            var tenantResource = tenants.Find(t => t.Data.TenantId == subscription.Data.TenantId);

            if (tenantResource is null)
            {
                continue;
            }

            var subscriptionResources =
                await _azureService.GetResourcesAsync(subscription, tenantResource, cancellationToken);
            cloudResources.AddRange(subscriptionResources);
        }

        // Use org-specific cache keys
        var orgId = configuration.OrganisationId.Value;
        _memoryCache.Set($"Azure:CloudResources:{orgId}", cloudResources);
    }
}