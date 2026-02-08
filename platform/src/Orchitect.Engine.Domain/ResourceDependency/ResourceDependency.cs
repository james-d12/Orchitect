namespace Orchitect.Engine.Domain.ResourceDependency;

public readonly record struct ResourceDependencyId(Guid Value)
{
    public ResourceDependencyId() : this(Guid.NewGuid())
    {
    }
}

public sealed record ResourceDependency
{
    public ResourceDependencyId Id { get; init; }
    public string Identifier { get; init; }

    public ResourceDependency(string Identifier)
    {
        Id = new ResourceDependencyId();
        this.Identifier = Identifier;
    }
}