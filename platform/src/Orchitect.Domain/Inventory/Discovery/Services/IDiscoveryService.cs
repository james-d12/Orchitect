using Orchitect.Domain.Core.Credential;

namespace Orchitect.Domain.Inventory.Discovery.Services;

public interface IDiscoveryService
{
    DiscoveryPlatform Platform { get; }

    /// <summary>
    /// Discovers and saves the platform's items, returning how many of each kind were found.
    /// </summary>
    Task<DiscoveryCounts> DiscoverAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken);
}