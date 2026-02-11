namespace Orchitect.Engine.Domain.Service;

public readonly record struct ServiceId(Guid Value)
{
    public ServiceId() : this(Guid.NewGuid())
    {
    }
}