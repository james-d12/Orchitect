namespace Conductor.Engine.Domain.Application;

public readonly record struct ApplicationId(Guid Value)
{
    public ApplicationId() : this(Guid.NewGuid())
    {
    }
}