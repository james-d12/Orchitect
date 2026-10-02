using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class ResourceDependencyGraphTests
{
    private readonly ResourceDependencyGraph _graph =
        ResourceDependencyGraph.Create(new OrganisationId(), new EnvironmentId(Guid.NewGuid()));

    private readonly ResourceId _app = new();
    private readonly ResourceId _vault = new();
    private readonly ResourceId _storage = new();
    private readonly ResourceId _other = new();

    public ResourceDependencyGraphTests()
    {
        foreach (var id in new[] { _app, _vault, _storage, _other })
        {
            _graph.AddResource(id);
        }
    }

    [Fact]
    public void SetDependencies_ReplacesOutgoingEdges()
    {
        _graph.AddDependency(_app, _vault);

        _graph.SetDependencies(_app, [_storage]);

        Assert.False(_graph.HasDependencyPath(_app, _vault));
        Assert.True(_graph.HasDependencyPath(_app, _storage));
        Assert.Equal(0, _graph.DependentCount(_vault));
    }

    [Fact]
    public void SetDependencies_LeavesOtherResourcesEdgesIntact()
    {
        _graph.AddDependency(_other, _vault);

        _graph.SetDependencies(_app, []);

        Assert.True(_graph.HasDependencyPath(_other, _vault));
    }

    [Fact]
    public void SetDependencies_UnknownResource_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _graph.SetDependencies(new ResourceId(), [_vault]));
    }

    [Fact]
    public void SetDependencies_WouldCreateCycle_Throws()
    {
        _graph.AddDependency(_vault, _app);

        Assert.Throws<InvalidOperationException>(() => _graph.SetDependencies(_app, [_vault]));
    }
}
