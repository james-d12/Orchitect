using System.Text.Json;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Unit.Tests.Engine;

public sealed class ResourceInstanceTests
{
    [Fact]
    public void Reconfigure_Active_ReplacesVersionAndInputs()
    {
        var instance = Active();
        var versionId = new ResourceTemplateVersionId();
        var inputs = new Dictionary<string, JsonElement> { ["sku"] = JsonSerializer.SerializeToElement("GRS") };

        instance.Reconfigure(versionId, inputs);

        Assert.Equal(versionId, instance.TemplateVersionId);
        Assert.Equal("GRS", instance.InputParameters["sku"].GetString());
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
    }

    [Theory]
    [InlineData(ResourceInstanceStatus.Removing)]
    [InlineData(ResourceInstanceStatus.Removed)]
    public void Reconfigure_BeingOrAlreadyRemoved_Throws(ResourceInstanceStatus status)
    {
        var instance = Active();
        instance.Transition(ResourceInstanceStatus.PendingRemoval);
        instance.Transition(ResourceInstanceStatus.Removing);
        if (status == ResourceInstanceStatus.Removed)
        {
            instance.Transition(ResourceInstanceStatus.Removed);
        }

        Assert.Throws<InvalidOperationException>(() =>
            instance.Reconfigure(new ResourceTemplateVersionId(), new Dictionary<string, JsonElement>()));
    }

    [Fact]
    public void Transition_FailedToPendingRemoval_IsAllowed()
    {
        var instance = Pending();
        instance.Transition(ResourceInstanceStatus.Provisioning);
        instance.Transition(ResourceInstanceStatus.Failed);

        instance.Transition(ResourceInstanceStatus.PendingRemoval);

        Assert.Equal(ResourceInstanceStatus.PendingRemoval, instance.Status);
    }

    [Fact]
    public void Transition_PendingToPendingRemoval_IsAllowed()
    {
        var instance = Pending();

        instance.Transition(ResourceInstanceStatus.PendingRemoval);

        Assert.Equal(ResourceInstanceStatus.PendingRemoval, instance.Status);
    }

    [Fact]
    public void RemoveConsumer_ExistingConsumer_RemovesIt()
    {
        var resource = Resource.Create(new CreateResourceRequest(new OrganisationId(), "orders-storage",
            string.Empty, new ResourceTemplateId(), new EnvironmentId(Guid.NewGuid()), ResourceKind.Direct));
        var applicationId = new ApplicationId();
        resource.AddConsumer(applicationId);

        resource.RemoveConsumer(applicationId);

        Assert.Empty(resource.Consumers);
    }

    [Fact]
    public void CreateSlug_NameWithSpacesAndCapitals_IsLowerKebabCase()
    {
        Assert.Equal("orders-shared-storage", Resource.CreateSlug("Orders Shared-Storage"));
    }

    private static ResourceInstance Pending() =>
        ResourceInstance.Create(new CreateResourceInstanceRequest(
            ResourceId: new ResourceId(),
            OrganisationId: new OrganisationId(),
            Name: "orders-storage-instance",
            TemplateVersionId: new ResourceTemplateVersionId(),
            EnvironmentId: new EnvironmentId(Guid.NewGuid())));

    private static ResourceInstance Active()
    {
        var instance = Pending();
        instance.Transition(ResourceInstanceStatus.Provisioning);
        instance.Transition(ResourceInstanceStatus.Active,
            new ResourceInstanceOutput { Location = new Uri("https://example.com") });
        return instance;
    }
}
