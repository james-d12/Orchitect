using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class ResourceDependencyGraphTests
{
    private readonly ResourceDependencyGraph _graph =
        ResourceDependencyGraph.Create(new OrganisationId(), new EnvironmentId());

    [Fact]
    public void AddResource_Twice_KeepsExistingDependencies()
    {
        var (app, db) = (Add(), Add());
        _graph.AddDependency(app, db);

        _graph.AddResource(app);

        Assert.True(_graph.ContainsResource(app));
        Assert.Equal(1, _graph.DependencyCount(app));
    }

    [Fact]
    public void AddDependency_RecordsBothDirections()
    {
        var (app, db) = (Add(), Add());

        _graph.AddDependency(app, db);

        Assert.Equal(1, _graph.DependencyCount(app));
        Assert.Equal(0, _graph.DependentCount(app));
        Assert.Equal(0, _graph.DependencyCount(db));
        Assert.Equal(1, _graph.DependentCount(db));
    }

    [Fact]
    public void AddDependency_OnItself_Throws()
    {
        var app = Add();

        Assert.Throws<ArgumentException>(() => _graph.AddDependency(app, app));
        Assert.Equal(0, _graph.DependencyCount(app));
    }

    [Fact]
    public void AddDependency_UnknownResource_Throws()
    {
        var app = Add();

        Assert.Throws<KeyNotFoundException>(() => _graph.AddDependency(app, new ResourceId()));
        Assert.Throws<KeyNotFoundException>(() => _graph.AddDependency(new ResourceId(), app));
        Assert.Equal(0, _graph.DependencyCount(app));
    }

    [Fact]
    public void AddDependency_DirectCycle_ThrowsAndLeavesGraphUnchanged()
    {
        var (app, db) = (Add(), Add());
        _graph.AddDependency(app, db);

        Assert.Throws<InvalidOperationException>(() => _graph.AddDependency(db, app));
        Assert.Equal(0, _graph.DependencyCount(db));
        Assert.Equal(0, _graph.DependentCount(app));
    }

    [Fact]
    public void AddDependency_IndirectCycle_ThrowsAndLeavesGraphUnchanged()
    {
        var (aks, subnet, vnet) = (Add(), Add(), Add());
        _graph.AddDependency(aks, subnet);
        _graph.AddDependency(subnet, vnet);

        Assert.Throws<InvalidOperationException>(() => _graph.AddDependency(vnet, aks));
        Assert.Equal(0, _graph.DependencyCount(vnet));
        Assert.Equal(0, _graph.DependentCount(aks));
        Assert.Equal([vnet, subnet, aks], _graph.ResolveOrder());
    }

    [Fact]
    public void AddDependency_Diamond_IsNotACycle()
    {
        var (aks, subnet, keyVault, vnet) = (Add(), Add(), Add(), Add());
        _graph.AddDependency(aks, subnet);
        _graph.AddDependency(aks, keyVault);
        _graph.AddDependency(subnet, vnet);

        _graph.AddDependency(keyVault, vnet);

        Assert.Equal(2, _graph.DependentCount(vnet));
    }

    [Fact]
    public void AddDependency_Twice_CountsOnce()
    {
        var (app, db) = (Add(), Add());

        _graph.AddDependency(app, db);
        _graph.AddDependency(app, db);

        Assert.Equal(1, _graph.DependencyCount(app));
        Assert.Equal(1, _graph.DependentCount(db));
    }

    [Fact]
    public void SetDependencies_ReplacesOutgoingEdges()
    {
        var (app, vault, storage) = (Add(), Add(), Add());
        _graph.AddDependency(app, vault);

        _graph.SetDependencies(app, [storage]);

        Assert.False(_graph.HasDependencyPath(app, vault));
        Assert.True(_graph.HasDependencyPath(app, storage));
        Assert.Equal(0, _graph.DependentCount(vault));
    }

    [Fact]
    public void SetDependencies_LeavesOtherResourcesEdgesIntact()
    {
        var (app, vault, other) = (Add(), Add(), Add());
        _graph.AddDependency(other, vault);

        _graph.SetDependencies(app, []);

        Assert.True(_graph.HasDependencyPath(other, vault));
    }

    [Fact]
    public void SetDependencies_UnknownResource_Throws()
    {
        var vault = Add();

        Assert.Throws<KeyNotFoundException>(() => _graph.SetDependencies(new ResourceId(), [vault]));
    }

    [Fact]
    public void SetDependencies_WouldCreateCycle_Throws()
    {
        var (app, vault) = (Add(), Add());
        _graph.AddDependency(vault, app);

        Assert.Throws<InvalidOperationException>(() => _graph.SetDependencies(app, [vault]));
    }

    [Fact]
    public void HasDependencyPath_FollowsDependenciesTransitively()
    {
        var (aks, subnet, vnet, unrelated) = (Add(), Add(), Add(), Add());
        _graph.AddDependency(aks, subnet);
        _graph.AddDependency(subnet, vnet);

        Assert.True(_graph.HasDependencyPath(aks, vnet));
        Assert.True(_graph.HasDependencyPath(aks, aks));
        Assert.False(_graph.HasDependencyPath(vnet, aks));
        Assert.False(_graph.HasDependencyPath(aks, unrelated));
        Assert.False(_graph.HasDependencyPath(aks, new ResourceId()));
    }

    [Fact]
    public void RemoveDependency_Existing_RemovesBothDirectionsAndAllowsReverse()
    {
        var (app, db) = (Add(), Add());
        _graph.AddDependency(app, db);

        Assert.True(_graph.RemoveDependency(app, db));

        Assert.Equal(0, _graph.DependencyCount(app));
        Assert.Equal(0, _graph.DependentCount(db));
        _graph.AddDependency(db, app);
    }

    [Fact]
    public void RemoveDependency_Missing_ReturnsFalse()
    {
        var (app, db) = (Add(), Add());
        _graph.AddDependency(app, db);

        Assert.False(_graph.RemoveDependency(db, app));
        Assert.False(_graph.RemoveDependency(app, new ResourceId()));
        Assert.Equal(1, _graph.DependencyCount(app));
    }

    [Fact]
    public void DependentCount_CountsResourcesThatDependOnIt()
    {
        var (cosmos, redis, subnet) = (Add(), Add(), Add());
        _graph.AddDependency(cosmos, subnet);
        _graph.AddDependency(redis, subnet);

        Assert.Equal(2, _graph.DependentCount(subnet));
        Assert.Equal(0, _graph.DependentCount(cosmos));
        Assert.Equal(0, _graph.DependentCount(new ResourceId()));
        Assert.Equal(0, _graph.DependencyCount(new ResourceId()));
    }

    [Fact]
    public void RemoveResource_Leaf_DetachesItFromItsDependencies()
    {
        var (cosmos, redis, subnet) = (Add(), Add(), Add());
        _graph.AddDependency(cosmos, subnet);
        _graph.AddDependency(redis, subnet);

        Assert.True(_graph.RemoveResource(cosmos));

        Assert.False(_graph.ContainsResource(cosmos));
        Assert.Equal(1, _graph.DependentCount(subnet));
        Assert.False(_graph.HasDependencyPath(cosmos, subnet));
    }

    [Fact]
    public void RemoveResource_WithDependents_DetachesThemToo()
    {
        var (aks, subnet, vnet) = (Add(), Add(), Add());
        _graph.AddDependency(aks, subnet);
        _graph.AddDependency(subnet, vnet);

        Assert.True(_graph.RemoveResource(subnet));

        Assert.Equal(0, _graph.DependencyCount(aks));
        Assert.Equal(0, _graph.DependentCount(vnet));
        Assert.Equal(2, _graph.ResolveOrder().Count);
    }

    [Fact]
    public void RemoveResource_Unknown_ReturnsFalse()
    {
        Assert.False(_graph.RemoveResource(new ResourceId()));
    }

    [Fact]
    public void GetDependenciesAndDependents_ReturnOnlyDirectNeighbours()
    {
        var (app, api, db) = (Add(), Add(), Add());
        _graph.AddDependency(app, api);
        _graph.AddDependency(api, db);

        Assert.Equal([db], _graph.GetDependencies(api));
        Assert.Equal([app], _graph.GetDependents(api));
        Assert.Empty(_graph.GetDependencies(db));
        Assert.Empty(_graph.GetDependents(app));
    }

    [Fact]
    public void GetDependenciesAndDependents_UnknownResource_ReturnEmpty()
    {
        var unknown = new ResourceId();

        Assert.Empty(_graph.GetDependencies(unknown));
        Assert.Empty(_graph.GetDependents(unknown));
    }

    [Fact]
    public void ResolveOrder_PutsDependenciesBeforeDependents()
    {
        var (aks, acr, keyVault, subnet, vnet) = (Add(), Add(), Add(), Add(), Add());
        _graph.AddDependency(aks, subnet);
        _graph.AddDependency(aks, acr);
        _graph.AddDependency(aks, keyVault);
        _graph.AddDependency(acr, keyVault);
        _graph.AddDependency(keyVault, subnet);
        _graph.AddDependency(subnet, vnet);

        var order = _graph.ResolveOrder();

        Assert.Equal([vnet, subnet, keyVault, acr, aks], order);
    }

    [Fact]
    public void ResolveOrder_IncludesUnconnectedResources()
    {
        var (app, db, standalone) = (Add(), Add(), Add());
        _graph.AddDependency(app, db);

        var order = _graph.ResolveOrder();

        Assert.Equal(3, order.Count);
        Assert.Contains(standalone, order);
        Assert.True(order.IndexOf(db) < order.IndexOf(app));
    }

    [Fact]
    public void ResolveOrder_Empty_ReturnsEmpty()
    {
        Assert.Empty(_graph.ResolveOrder());
    }

    private ResourceId Add()
    {
        var id = new ResourceId();
        _graph.AddResource(id);
        return id;
    }
}
