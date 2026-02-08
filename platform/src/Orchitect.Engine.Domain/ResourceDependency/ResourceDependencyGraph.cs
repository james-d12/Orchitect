namespace Orchitect.Engine.Domain.ResourceDependency;

public sealed class ResourceDependencyGraph : IResourceDependencyGraph
{
    private readonly Dictionary<ResourceDependencyId, ResourceDependencyNode> _nodes = new();

    public int DependentCount(ResourceDependencyId dependencyId)
    {
        return _nodes.TryGetValue(dependencyId, out ResourceDependencyNode? node) ? node.In.Count : 0;
    }

    public int DependencyCount(ResourceDependencyId dependencyId)
    {
        return _nodes.TryGetValue(dependencyId, out ResourceDependencyNode? node) ? node.Out.Count : 0;
    }

    public void AddResource(ResourceDependency resourceDependency)
    {
        if (!_nodes.ContainsKey(resourceDependency.Id))
        {
            _nodes[resourceDependency.Id] = new ResourceDependencyNode { Value = resourceDependency };
        }
    }

    public bool RemoveResource(ResourceDependencyId dependencyId)
    {
        if (!_nodes.TryGetValue(dependencyId, out ResourceDependencyNode? node))
        {
            return false;
        }

        foreach (ResourceDependencyId fromId in node.In)
        {
            _nodes[fromId].Out.Remove(dependencyId);
        }

        foreach (ResourceDependencyId toId in node.Out)
        {
            _nodes[toId].In.Remove(dependencyId);
        }

        _nodes.Remove(dependencyId);
        return true;
    }

    public void AddDependency(ResourceDependencyId from, ResourceDependencyId to)
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

    public bool RemoveDependency(ResourceDependencyId from, ResourceDependencyId to)
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

    public bool HasDependencyPath(ResourceDependencyId startId, ResourceDependencyId targetId)
    {
        if (!_nodes.ContainsKey(startId) || !_nodes.ContainsKey(targetId))
        {
            return false;
        }

        if (startId.Equals(targetId))
        {
            return true;
        }

        var stack = new Stack<ResourceDependencyId>();
        var visited = new HashSet<ResourceDependencyId>();

        stack.Push(startId);
        visited.Add(startId);

        while (stack.Count > 0)
        {
            ResourceDependencyId currentId = stack.Pop();
            ResourceDependencyNode resourceDependencyNode = _nodes[currentId];

            foreach (ResourceDependencyId neighborId in resourceDependencyNode.Out.Where(id => !visited.Contains(id)))
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

    public IList<ResourceDependency> ResolveOrder()
    {
        var inDegreeMap = new Dictionary<ResourceDependencyId, int>();
        var zeroInDegreeQueue = new Queue<ResourceDependencyId>();

        foreach ((ResourceDependencyId resourceId, ResourceDependencyNode node) in _nodes)
        {
            inDegreeMap[resourceId] = node.In.Count;
            if (node.In.Count == 0)
            {
                zeroInDegreeQueue.Enqueue(resourceId);
            }
        }

        var sortedResources = new List<ResourceDependency>(_nodes.Count);

        while (zeroInDegreeQueue.Count > 0)
        {
            ResourceDependencyId currentId = zeroInDegreeQueue.Dequeue();
            ResourceDependencyNode resourceDependencyNode = _nodes[currentId];
            sortedResources.Add(resourceDependencyNode.Value);

            foreach (ResourceDependencyId dependentId in resourceDependencyNode.Out)
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