using Orchitect.Domain.Core.Credential;

namespace Orchitect.Domain.Inventory.Discovery.Services;

public interface IDiscoveryService
{
    DiscoveryPlatform Platform { get; }

    Task DiscoverAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken);
}