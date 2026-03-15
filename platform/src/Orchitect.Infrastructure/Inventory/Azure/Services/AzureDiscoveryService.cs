using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Cloud.Service;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.Azure.Services;

public sealed class AzureDiscoveryService : DiscoveryService
{
    private readonly ILogger<AzureDiscoveryService> _logger;
    private readonly CredentialPayloadResolver _payloadResolver;
    private readonly ICloudResourceRepository _cloudResourceRepository;

    public AzureDiscoveryService(
        ILogger<AzureDiscoveryService> logger,
        CredentialPayloadResolver payloadResolver,
        ICloudResourceRepository cloudResourceRepository) : base(logger)
    {
        _logger = logger;
        _payloadResolver = payloadResolver;
        _cloudResourceRepository = cloudResourceRepository;
    }

    public override string Platform => "Azure";

    protected override async Task StartAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        // Create connection service from credential
        var connectionService = AzureConnectionService.FromCredential(
            credential,
            _payloadResolver,
            configuration.PlatformConfig);

        // Create Azure service with this connection
        var azureService = new AzureService(connectionService);

        _logger.LogInformation("Discovering Azure Tenant resources...");
        var tenants = await azureService.GetTenantsAsync(cancellationToken);

        _logger.LogInformation("Discovering Azure Subscription resources.");

        // Parse subscription filters from platform config (comma-separated)
        var subscriptionFilters = configuration.PlatformConfig.TryGetValue("subscriptionFilters", out var filters)
            ? filters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : new List<string>();

        var subscriptions =
            await azureService.GetSubscriptionsAsync(subscriptionFilters, cancellationToken);

        var cloudResources = new List<Domain.Inventory.Cloud.CloudResource>();

        foreach (var subscription in subscriptions)
        {
            var tenantResource = tenants.Find(t => t.Data.TenantId == subscription.Data.TenantId);

            if (tenantResource is null)
            {
                continue;
            }

            var subscriptionResources =
                await azureService.GetResourcesAsync(subscription, tenantResource, configuration.OrganisationId, cancellationToken);
            cloudResources.AddRange(subscriptionResources);
        }

        // Persist all discovered cloud resources to database
        await _cloudResourceRepository.BulkUpsertAsync(cloudResources, cancellationToken);

        _logger.LogInformation(
            "Azure discovery completed for organisation {OrganisationId}: {CloudResourceCount} cloud resources",
            configuration.OrganisationId.Value,
            cloudResources.Count);
    }
}