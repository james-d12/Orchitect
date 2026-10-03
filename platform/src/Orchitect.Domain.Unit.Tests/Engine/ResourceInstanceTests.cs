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
    private static readonly Dictionary<ResourceInstanceStatus, ResourceInstanceStatus[]> AllowedTransitions = new()
    {
        [ResourceInstanceStatus.Pending] = [ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.PendingRemoval],
        [ResourceInstanceStatus.Provisioning] = [ResourceInstanceStatus.Active, ResourceInstanceStatus.Failed],
        [ResourceInstanceStatus.Active] = [ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.PendingRemoval],
        [ResourceInstanceStatus.Failed] = [ResourceInstanceStatus.Pending, ResourceInstanceStatus.PendingRemoval],
        [ResourceInstanceStatus.PendingRemoval] = [ResourceInstanceStatus.Removing],
        [ResourceInstanceStatus.Removing] = [ResourceInstanceStatus.Removed, ResourceInstanceStatus.RemovalFailed],
        [ResourceInstanceStatus.Removed] = [],
        [ResourceInstanceStatus.RemovalFailed] = [ResourceInstanceStatus.PendingRemoval]
    };

    private static readonly Dictionary<ResourceInstanceStatus, ResourceInstanceStatus[]> PathFromPending = new()
    {
        [ResourceInstanceStatus.Pending] = [],
        [ResourceInstanceStatus.Provisioning] = [ResourceInstanceStatus.Provisioning],
        [ResourceInstanceStatus.Active] = [ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.Active],
        [ResourceInstanceStatus.Failed] = [ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.Failed],
        [ResourceInstanceStatus.PendingRemoval] =
        [
            ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.Active, ResourceInstanceStatus.PendingRemoval
        ],
        [ResourceInstanceStatus.Removing] =
        [
            ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.Active, ResourceInstanceStatus.PendingRemoval,
            ResourceInstanceStatus.Removing
        ],
        [ResourceInstanceStatus.Removed] =
        [
            ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.Active, ResourceInstanceStatus.PendingRemoval,
            ResourceInstanceStatus.Removing, ResourceInstanceStatus.Removed
        ],
        [ResourceInstanceStatus.RemovalFailed] =
        [
            ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.Active, ResourceInstanceStatus.PendingRemoval,
            ResourceInstanceStatus.Removing, ResourceInstanceStatus.RemovalFailed
        ]
    };

    public static TheoryData<ResourceInstanceStatus, ResourceInstanceStatus> Allowed()
    {
        var data = new TheoryData<ResourceInstanceStatus, ResourceInstanceStatus>();
        foreach ((ResourceInstanceStatus from, ResourceInstanceStatus[] targets) in AllowedTransitions)
        {
            foreach (ResourceInstanceStatus to in targets)
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    public static TheoryData<ResourceInstanceStatus, ResourceInstanceStatus> Forbidden()
    {
        var data = new TheoryData<ResourceInstanceStatus, ResourceInstanceStatus>();
        foreach (ResourceInstanceStatus from in Enum.GetValues<ResourceInstanceStatus>())
        {
            foreach (ResourceInstanceStatus to in Enum.GetValues<ResourceInstanceStatus>()
                         .Where(to => !AllowedTransitions[from].Contains(to)))
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Fact]
    public void TransitionTable_CoversEveryStatus()
    {
        Assert.Equal(Enum.GetValues<ResourceInstanceStatus>().Order(), AllowedTransitions.Keys.Order());
        Assert.Equal(Enum.GetValues<ResourceInstanceStatus>().Order(), PathFromPending.Keys.Order());
    }

    [Fact]
    public void Create_StartsPendingWithNoOutput()
    {
        var instance = NewInstance();

        Assert.Equal(ResourceInstanceStatus.Pending, instance.Status);
        Assert.Null(instance.Output);
        Assert.Empty(instance.InputParameters);
    }

    [Fact]
    public void Create_KeepsInputParameters()
    {
        var parameters = new Dictionary<string, JsonElement>
        {
            ["sku"] = JsonSerializer.SerializeToElement("Standard")
        };

        var instance = ResourceInstance.Create(NewRequest() with { InputParameters = parameters });

        Assert.Equal("Standard", instance.InputParameters["sku"].GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_MissingName_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => ResourceInstance.Create(NewRequest() with { Name = name! }));
    }

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Transition_Allowed_ChangesStatus(ResourceInstanceStatus from, ResourceInstanceStatus to)
    {
        var instance = InStatus(from);
        var before = instance.UpdatedAt;

        instance.Transition(to, NewOutput());

        Assert.Equal(to, instance.Status);
        Assert.True(instance.UpdatedAt >= before);
    }

    [Theory]
    [MemberData(nameof(Forbidden))]
    public void Transition_Forbidden_ThrowsAndKeepsStatus(ResourceInstanceStatus from, ResourceInstanceStatus to)
    {
        var instance = InStatus(from);
        var output = instance.Output;

        Assert.Throws<InvalidOperationException>(() => instance.Transition(to, NewOutput()));
        Assert.Equal(from, instance.Status);
        Assert.Same(output, instance.Output);
    }

    [Fact]
    public void Transition_ToActiveWithoutOutput_ThrowsAndKeepsStatus()
    {
        var instance = InStatus(ResourceInstanceStatus.Provisioning);

        Assert.Throws<ArgumentNullException>(() => instance.Transition(ResourceInstanceStatus.Active));
        Assert.Equal(ResourceInstanceStatus.Provisioning, instance.Status);
        Assert.Null(instance.Output);
    }

    [Fact]
    public void Transition_ToActive_SetsOutput()
    {
        var instance = InStatus(ResourceInstanceStatus.Provisioning);
        var output = NewOutput();

        instance.Transition(ResourceInstanceStatus.Active, output);

        Assert.Same(output, instance.Output);
    }

    [Fact]
    public void Transition_WithoutOutput_KeepsPreviousOutput()
    {
        var instance = InStatus(ResourceInstanceStatus.Active);
        var output = instance.Output;

        instance.Transition(ResourceInstanceStatus.PendingRemoval);

        Assert.NotNull(output);
        Assert.Same(output, instance.Output);
    }

    [Fact]
    public void Transition_ReprovisionActive_ReplacesOutput()
    {
        var instance = InStatus(ResourceInstanceStatus.Active);
        var output = NewOutput();

        instance.Transition(ResourceInstanceStatus.Provisioning);
        instance.Transition(ResourceInstanceStatus.Active, output);

        Assert.Same(output, instance.Output);
    }

    [Fact]
    public void Transition_FailedRetry_CanProvisionAgain()
    {
        var instance = InStatus(ResourceInstanceStatus.Failed);

        instance.Transition(ResourceInstanceStatus.Pending);
        instance.Transition(ResourceInstanceStatus.Provisioning);
        instance.Transition(ResourceInstanceStatus.Active, NewOutput());

        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
    }

    [Fact]
    public void Transition_RemovalFailedRetry_CanBeRemoved()
    {
        var instance = InStatus(ResourceInstanceStatus.RemovalFailed);

        instance.Transition(ResourceInstanceStatus.PendingRemoval);
        instance.Transition(ResourceInstanceStatus.Removing);
        instance.Transition(ResourceInstanceStatus.Removed);

        Assert.Equal(ResourceInstanceStatus.Removed, instance.Status);
    }

    [Fact]
    public void Reconfigure_Active_ReplacesVersionAndInputs()
    {
        var instance = InStatus(ResourceInstanceStatus.Active);
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
        var instance = InStatus(status);

        Assert.Throws<InvalidOperationException>(() =>
            instance.Reconfigure(new ResourceTemplateVersionId(), new Dictionary<string, JsonElement>()));
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

    private static ResourceInstance InStatus(ResourceInstanceStatus status)
    {
        var instance = NewInstance();
        foreach (ResourceInstanceStatus step in PathFromPending[status])
        {
            instance.Transition(step, step == ResourceInstanceStatus.Active ? NewOutput() : null);
        }

        Assert.Equal(status, instance.Status);
        return instance;
    }

    private static ResourceInstance NewInstance() => ResourceInstance.Create(NewRequest());

    private static CreateResourceInstanceRequest NewRequest() =>
        new(new ResourceId(), new OrganisationId(), "cosmos-orders", new ResourceTemplateVersionId(),
            new EnvironmentId());

    private static ResourceInstanceOutput NewOutput() =>
        new() { Location = new Uri($"https://state.example.com/{Guid.NewGuid()}") };
}
