using System.Collections.Concurrent;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Secrets;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Infrastructure.Inventory.Azure.Extensions;
using Orchitect.Infrastructure.Inventory.Azure.Models;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.Azure.Services;

public sealed class AzureService : IAzureService
{
    private readonly ArmClient _client;

    public AzureService(IAzureConnectionService connectionService)
    {
        _client = connectionService.Client;
    }

    public async Task<List<TenantResource>> GetTenantsAsync(CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        var tenants = new List<TenantResource>();
        await foreach (var tenant in _client.GetTenants().GetAllAsync(cancellationToken))
        {
            tenants.Add(tenant);
        }

        return tenants;
    }

    public async Task<List<SubscriptionResource>> GetSubscriptionsAsync(
        List<string> subscriptionFilters,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        var subscriptionResources = new List<SubscriptionResource>();

        await foreach (var subscription in _client.GetSubscriptions().GetAllAsync(cancellationToken))
        {
            subscriptionResources.Add(subscription);
        }

        if (subscriptionFilters.Count <= 0)
        {
            return subscriptionResources;
        }

        return subscriptionResources
            .Where(p => subscriptionFilters.Contains(p.Data.DisplayName, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<List<AzureCloudResource>> GetResourcesAsync(
        SubscriptionResource subscriptionResource,
        TenantResource tenantResource,
        OrganisationId organisationId,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        var azureResources = new List<AzureCloudResource>();

        await foreach (var resource in subscriptionResource.GetGenericResourcesAsync(
                           cancellationToken: cancellationToken))
        {
            var azureResource = resource.Data.MapToAzureResource(
                tenantResource.Data.DisplayName,
                subscriptionResource.Data.DisplayName,
                organisationId);
            azureResources.Add(azureResource);
        }

        return azureResources;
    }

    public async Task<List<CloudSecret>> GetKeyVaultSecretsAsync(List<AzureCloudResource> resources,
        OrganisationId organisationId,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        var cloudSecrets = new ConcurrentBag<CloudSecret>();
        var vaults = resources.Where(r => r.Type.EqualsCaseInsensitive("vaults")).ToList();
        var now = DateTime.UtcNow;

        var tasks = vaults.Select(async vault =>
        {
            try
            {
                var vaultUri = new Uri($"https://{vault.Id.Value}.vault.azure.net/");
                var client = new SecretClient(vaultUri, new DefaultAzureCredential());

                foreach (var secret in client.GetPropertiesOfSecrets())
                {
                    // Generate unique ID from vault URI + secret name
                    var secretId = $"{secret.VaultUri}/{secret.Name}";

                    cloudSecrets.Add(new CloudSecret
                    {
                        Id = new CloudSecretId(secretId),
                        OrganisationId = organisationId,
                        Name = secret.Name,
                        Location = vault.Name,
                        Url = secret.VaultUri,
                        Platform = CloudSecretPlatform.Azure,
                        DiscoveredAt = now,
                        UpdatedAt = now
                    });
                }

                await Task.CompletedTask;
            }
            catch (Exception)
            {
                // Log exception if necessary
                // Log.Error(ex, "Error fetching secrets for vault: {VaultName}", vault.Name);
            }
        }).ToList();

        await Task.WhenAll(tasks);

        return cloudSecrets.ToList();
    }
}