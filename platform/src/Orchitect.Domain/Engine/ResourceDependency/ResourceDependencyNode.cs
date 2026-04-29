using Orchitect.Domain.Engine.Resource;

namespace Orchitect.Domain.Engine.ResourceDependency;

public sealed record ResourceDependencyNode
{
    public required ResourceId ResourceId { get; init; }
    public readonly HashSet<ResourceId> In = [];
    public readonly HashSet<ResourceId> Out = [];
}
