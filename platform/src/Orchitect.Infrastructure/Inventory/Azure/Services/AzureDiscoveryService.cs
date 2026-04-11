using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Cloud.Services;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Infrastructure.Inventory.Shared;

namespace Orchitect.Infrastructure.Inventory.Azure.Services;

public sealed class AzureDiscoveryService : DiscoveryService
{
    private readonly ILogger<AzureDiscoveryService> _logger;
    private readonly CredentialPayloadResolver _payloadResolver;
    private readonly ICloudResourceRepository _cloudResourceRepository;
    private readonly ICloudSecretRepository _cloudSecretRepository;

    public AzureDiscoveryService(
        ILogger<AzureDiscoveryService> logger,
        CredentialPayloadResolver payloadResolver,
        ICloudResourceRepository cloudResourceRepository,
        ICloudSecretRepository cloudSecretRepository) : base(logger)
    {
        _logger = logger;
        _payloadResolver = payloadResolver;
        _cloudResourceRepository = cloudResourceRepository;
        _cloudSecretRepository = cloudSecretRepository;
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

        var azureCloudResources = new List<Infrastructure.Inventory.Azure.Models.AzureCloudResource>();

        foreach (var subscription in subscriptions)
        {
            var tenantResource = tenants.Find(t => t.Data.TenantId == subscription.Data.TenantId);

            if (tenantResource is null)
            {
                continue;
            }

            var subscriptionResources =
                await azureService.GetResourcesAsync(subscription, tenantResource, configuration.OrganisationId, cancellationToken);
            azureCloudResources.AddRange(subscriptionResources);
        }

        // Persist all discovered cloud resources to database
        await _cloudResourceRepository.BulkUpsertAsync(azureCloudResources, cancellationToken);

        // Discover and persist cloud secrets from Key Vaults
        _logger.LogInformation("Discovering Azure Key Vault secrets...");
        var cloudSecrets = await azureService.GetKeyVaultSecretsAsync(
            azureCloudResources,
            configuration.OrganisationId,
            cancellationToken);

        await _cloudSecretRepository.BulkUpsertAsync(cloudSecrets, cancellationToken);

        _logger.LogInformation(
            "Azure discovery completed for organisation {OrganisationId}: {CloudResourceCount} cloud resources, {CloudSecretCount} cloud secrets",
            configuration.OrganisationId.Value,
            azureCloudResources.Count,
            cloudSecrets.Count);
    }
}