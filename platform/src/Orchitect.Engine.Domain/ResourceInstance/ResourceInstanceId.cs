namespace Orchitect.Engine.Domain.ResourceInstance;

public readonly record struct ResourceInstanceId(Guid Value)
{
    public ResourceInstanceId() : this(Guid.NewGuid())
    {
    }
}