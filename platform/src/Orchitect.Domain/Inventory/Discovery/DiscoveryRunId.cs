namespace Orchitect.Domain.Inventory.Discovery;

public readonly record struct DiscoveryRunId(Guid Value)
{
    public DiscoveryRunId() : this(Guid.NewGuid())
    {
    }
}
