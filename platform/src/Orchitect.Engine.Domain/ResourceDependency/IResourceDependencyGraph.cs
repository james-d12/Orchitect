namespace Orchitect.Engine.Domain.ResourceDependency;

public interface IResourceDependencyGraph
{
    int DependentCount(ResourceDependencyId nodeId);
    int DependencyCount(ResourceDependencyId nodeId);
    void AddResource(ResourceDependency resourceDependency);
    bool RemoveResource(ResourceDependencyId nodeId);
    void AddDependency(ResourceDependencyId from, ResourceDependencyId to);
    bool RemoveDependency(ResourceDependencyId from, ResourceDependencyId to);
    bool HasDependencyPath(ResourceDependencyId startId, ResourceDependencyId targetId);
    IList<ResourceDependency> ResolveOrder();
}