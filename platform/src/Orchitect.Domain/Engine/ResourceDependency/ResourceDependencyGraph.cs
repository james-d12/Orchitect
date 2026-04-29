using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;

namespace Orchitect.Domain.Engine.ResourceDependency;

public sealed class ResourceDependencyGraph : IResourceDependencyGraph
{
    private readonly Dictionary<ResourceId, ResourceDependencyNode> _nodes = [];

    public ResourceDependencyGraphId Id { get; private init; }
    public OrganisationId OrganisationId { get; private init; }
    public EnvironmentId EnvironmentId { get; private init; }

    private ResourceDependencyGraph() { }

    public static ResourceDependencyGraph Create(OrganisationId organisationId, EnvironmentId environmentId)
        => new() { Id = new ResourceDependencyGraphId(), OrganisationId = organisationId, EnvironmentId = environmentId };

    public int DependentCount(ResourceId resourceId)
    {
        return _nodes.TryGetValue(resourceId, out ResourceDependencyNode? node) ? node.In.Count : 0;
    }

    public int DependencyCount(ResourceId resourceId)
    {
        return _nodes.TryGetValue(resourceId, out ResourceDependencyNode? node) ? node.Out.Count : 0;
    }

    public void AddResource(ResourceId resourceId)
    {
        if (!_nodes.ContainsKey(resourceId))
        {
            _nodes[resourceId] = new ResourceDependencyNode { ResourceId = resourceId };
        }
    }

    public bool RemoveResource(ResourceId resourceId)
    {
        if (!_nodes.TryGetValue(resourceId, out ResourceDependencyNode? node))
        {
            return false;
        }

        foreach (ResourceId fromId in node.In)
        {
            _nodes[fromId].Out.Remove(resourceId);
        }

        foreach (ResourceId toId in node.Out)
        {
            _nodes[toId].In.Remove(resourceId);
        }

        _nodes.Remove(resourceId);
        return true;
    }

    public void AddDependency(ResourceId from, ResourceId to)
    {
        if (from.Equals(to))
        {
            throw new ArgumentException("Cannot add a dependency to itself.");
        }

        if (!_nodes.ContainsKey(from) || !_nodes.ContainsKey(to))
        {
            throw new KeyNotFoundException("Both resources must exist in the graph.");
        }

        if (HasDependencyPath(to, from))
        {
            throw new InvalidOperationException("Adding this dependency would create a cycle.");
        }

        _nodes[from].Out.Add(to);
        _nodes[to].In.Add(from);
    }

    public bool RemoveDependency(ResourceId from, ResourceId to)
    {
        if (!_nodes.TryGetValue(from, out ResourceDependencyNode? fromNode) ||
            !_nodes.TryGetValue(to, out ResourceDependencyNode? toNode))
        {
            return false;
        }

        var removed = fromNode.Out.Remove(to);
        if (removed)
        {
            toNode.In.Remove(from);
        }

        return removed;
    }

    public bool HasDependencyPath(ResourceId startId, ResourceId targetId)
    {
        if (!_nodes.ContainsKey(startId) || !_nodes.ContainsKey(targetId))
        {
            return false;
        }

        if (startId.Equals(targetId))
        {
            return true;
        }

        var stack = new Stack<ResourceId>();
        var visited = new HashSet<ResourceId>();

        stack.Push(startId);
        visited.Add(startId);

        while (stack.Count > 0)
        {
            ResourceId currentId = stack.Pop();
            ResourceDependencyNode resourceDependencyNode = _nodes[currentId];

            foreach (ResourceId neighborId in resourceDependencyNode.Out.Where(id => !visited.Contains(id)))
            {
                if (neighborId.Equals(targetId))
                {
                    return true;
                }

                visited.Add(neighborId);
                stack.Push(neighborId);
            }
        }

        return false;
    }

    public bool ContainsResource(ResourceId resourceId)
    {
        return _nodes.ContainsKey(resourceId);
    }

    public IList<ResourceId> ResolveOrder()
    {
        var inDegreeMap = new Dictionary<ResourceId, int>();
        var zeroInDegreeQueue = new Queue<ResourceId>();

        foreach ((ResourceId resourceId, ResourceDependencyNode node) in _nodes)
        {
            inDegreeMap[resourceId] = node.In.Count;
            if (node.In.Count == 0)
            {
                zeroInDegreeQueue.Enqueue(resourceId);
            }
        }

        var sortedResources = new List<ResourceId>(_nodes.Count);

        while (zeroInDegreeQueue.Count > 0)
        {
            ResourceId currentId = zeroInDegreeQueue.Dequeue();
            ResourceDependencyNode resourceDependencyNode = _nodes[currentId];
            sortedResources.Add(currentId);

            foreach (ResourceId dependentId in resourceDependencyNode.Out)
            {
                inDegreeMap[dependentId]--;
                if (inDegreeMap[dependentId] == 0)
                {
                    zeroInDegreeQueue.Enqueue(dependentId);
                }
            }
        }

        if (sortedResources.Count != _nodes.Count)
        {
            throw new InvalidOperationException("Graph contains a cycle.");
        }

        return sortedResources;
    }
}
