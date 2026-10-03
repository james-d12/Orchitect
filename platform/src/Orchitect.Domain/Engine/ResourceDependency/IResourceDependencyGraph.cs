using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;

namespace Orchitect.Domain.Engine.ResourceDependency;

public interface IResourceDependencyGraph
{
    ResourceDependencyGraphId Id { get; }
    OrganisationId OrganisationId { get; }
    EnvironmentId EnvironmentId { get; }

    int DependentCount(ResourceId resourceId);
    int DependencyCount(ResourceId resourceId);
    void AddResource(ResourceId resourceId);
    bool RemoveResource(ResourceId resourceId);

    /// <summary>
    /// Records that <paramref name="from"/> depends on <paramref name="to"/>.
    /// </summary>
    void AddDependency(ResourceId from, ResourceId to);

    bool RemoveDependency(ResourceId from, ResourceId to);

    /// <summary>
    /// Replaces a resource's outgoing dependencies, leaving every other resource's edges intact.
    /// </summary>
    void SetDependencies(ResourceId from, IEnumerable<ResourceId> to);

    bool HasDependencyPath(ResourceId startId, ResourceId targetId);
    bool ContainsResource(ResourceId resourceId);

    /// <summary>
    /// Orders the resources so that every resource comes after the resources it depends on.
    /// </summary>
    IList<ResourceId> ResolveOrder();
}
