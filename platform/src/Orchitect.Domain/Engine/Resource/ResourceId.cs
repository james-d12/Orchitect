namespace Orchitect.Domain.Engine.Resource;

public readonly record struct ResourceId(Guid Value)
{
    public ResourceId() : this(Guid.NewGuid())
    {
    }
}