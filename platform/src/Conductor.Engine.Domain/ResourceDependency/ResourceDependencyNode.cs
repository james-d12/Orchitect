namespace Conductor.Engine.Domain.ResourceDependency;

public sealed record ResourceDependencyNode
{
    public required ResourceDependency Value { get; init; }
    public readonly HashSet<ResourceDependencyId> In = [];
    public readonly HashSet<ResourceDependencyId> Out = [];
}