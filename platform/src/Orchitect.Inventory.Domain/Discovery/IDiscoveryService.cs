using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Domain.Discovery;

public interface IDiscoveryService
{
    string Platform { get; }

    Task DiscoverAsync(
        DiscoveryConfiguration configuration,
        Credential credential,
        CancellationToken cancellationToken);
}