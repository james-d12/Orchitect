namespace Orchitect.Inventory.Domain.Discovery;

public interface IDiscoveryService
{
    string Platform { get; }
    Task DiscoveryAsync(CancellationToken cancellationToken);
}