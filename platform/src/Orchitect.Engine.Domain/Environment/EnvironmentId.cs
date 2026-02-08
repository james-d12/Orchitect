namespace Orchitect.Engine.Domain.Environment;

public readonly record struct EnvironmentId(Guid Value)
{
    public EnvironmentId() : this(Guid.NewGuid())
    {
    }
}