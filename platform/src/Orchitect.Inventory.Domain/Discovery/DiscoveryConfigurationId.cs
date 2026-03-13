namespace Orchitect.Inventory.Domain.Discovery;

public sealed record DiscoveryConfigurationId(Guid Value)
{
    public DiscoveryConfigurationId() : this(Guid.NewGuid()) { }
}
