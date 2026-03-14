namespace Orchitect.Domain.Engine.Application;

public readonly record struct ApplicationId(Guid Value)
{
    public ApplicationId() : this(Guid.NewGuid())
    {
    }
}