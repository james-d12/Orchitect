using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class ResourceTests
{
    [Fact]
    public void Rename_UpdatesNameSlugAndUpdatedAtButKeepsIdentity()
    {
        var resource = NewResource("orders-storage");
        var id = resource.Id;
        var updatedAt = resource.UpdatedAt;

        resource.Rename("Orders Data");

        Assert.Equal(id, resource.Id);
        Assert.Equal("Orders Data", resource.Name);
        Assert.Equal("orders-data", resource.Slug);
        Assert.True(resource.UpdatedAt >= updatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Rename_EmptyName_Throws(string? name)
    {
        var resource = NewResource("orders-storage");

        Assert.ThrowsAny<ArgumentException>(() => resource.Rename(name!));
        Assert.Equal("orders-storage", resource.Slug);
    }

    private static Resource NewResource(string name) =>
        Resource.Create(new CreateResourceRequest(new OrganisationId(), name, string.Empty,
            new ResourceTemplateId(), new EnvironmentId(Guid.NewGuid()), ResourceKind.Direct));
}
