using Azure.ResourceManager.Resources;
using Orchitect.Inventory.Domain.Cloud;
using Orchitect.Inventory.Infrastructure.Azure.Models;

namespace Orchitect.Inventory.Infrastructure.Azure.Services;

public interface IAzureService
{
    Task<List<TenantResource>> GetTenantsAsync(CancellationToken cancellationToken);

    Task<List<AzureCloudResource>> GetResourcesAsync(
        SubscriptionResource subscriptionResource,
        TenantResource tenantResource,
        CancellationToken cancellationToken);

    Task<List<SubscriptionResource>> GetSubscriptionsAsync(
        List<string> subscriptionFilters,
        CancellationToken cancellationToken);

    Task<List<CloudSecret>> GetKeyVaultSecretsAsync(List<AzureCloudResource> resources,
        CancellationToken cancellationToken);
}