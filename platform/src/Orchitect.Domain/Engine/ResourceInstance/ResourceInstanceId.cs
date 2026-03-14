namespace Orchitect.Domain.Engine.ResourceInstance;

public readonly record struct ResourceInstanceId(Guid Value)
{
    public ResourceInstanceId() : this(Guid.NewGuid())
    {
    }
}