namespace Orchitect.Domain.Engine.ResourceDependency;

public readonly record struct ResourceDependencyGraphId(Guid Value)
{
    public ResourceDependencyGraphId() : this(Guid.NewGuid())
    {
    }
}
