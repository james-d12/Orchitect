namespace Orchitect.Domain.Inventory.Discovery;

public sealed record DiscoveryConfigurationId(Guid Value)
{
    public DiscoveryConfigurationId() : this(Guid.NewGuid()) { }
}
