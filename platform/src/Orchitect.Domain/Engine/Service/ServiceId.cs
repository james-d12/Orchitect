namespace Orchitect.Domain.Engine.Service;

public readonly record struct ServiceId(Guid Value)
{
    public ServiceId() : this(Guid.NewGuid())
    {
    }
}