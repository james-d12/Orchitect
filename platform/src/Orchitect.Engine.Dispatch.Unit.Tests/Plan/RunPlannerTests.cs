using Microsoft.Extensions.Logging;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Plan;
using static Orchitect.Engine.Dispatch.Unit.Tests.Plan.RunTestContext;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Plan;

public sealed class RunPlannerTests
{
    private readonly RunTestContext _context = new();

    [Fact]
    public async Task PlanAsync_ResourceTemplateMissing_ThrowsWithoutRecording()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision,
                Score(("storage", "unknown-type", new() { ["name"] = "x" }))));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("unknown-type", exception.Message);
        Assert.Contains("storage", exception.Message);
        Assert.Empty(_context.Resources.Items);
        Assert.Empty(_context.Plans.Items);
    }

    [Fact]
    public async Task PlanAsync_TemplateHasNoActiveVersion_ThrowsWithoutRecording()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision, Score(("storage", InactiveType, null))));

        Assert.Contains("no active version", exception.Message);
        Assert.Empty(_context.Resources.Items);
    }

    [Fact]
    public async Task PlanAsync_NoResources_Throws()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Destroy, Score()));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
    }

    [Fact]
    public async Task PlanAsync_RunNotFound_Throws()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.CreatePlanner().PlanAsync(new DeploymentRunId(Guid.NewGuid()),
                Score(("storage", StorageType, null)), CancellationToken.None));

        Assert.Equal(RunPlanFailure.RunNotFound, exception.Failure);
    }

    [Fact]
    public async Task PlanAsync_RunNotRunning_Throws()
    {
        var run = _context.AddRun(DeploymentRun.Queue(_context.Deployment.Id, DeploymentRunOperation.Provision));

        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.CreatePlanner().PlanAsync(run.Id, Score(("storage", StorageType, null)), CancellationToken.None));

        Assert.Equal(RunPlanFailure.RunNotRunning, exception.Failure);
        Assert.Empty(_context.Plans.Items);
    }

    [Fact]
    public async Task PlanAsync_CancelRequested_ThrowsWithoutRecording()
    {
        var run = _context.AddRun(DeploymentRun.Queue(_context.Deployment.Id, DeploymentRunOperation.Provision)
            .Start().RequestCancel(DateTime.UtcNow));

        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.CreatePlanner().PlanAsync(run.Id, Score(("storage", StorageType, null)), CancellationToken.None));

        Assert.Equal(RunPlanFailure.RunNotRunning, exception.Failure);
        Assert.Empty(_context.Plans.Items);
        Assert.Empty(_context.Resources.Items);
        Assert.Empty(_context.Instances.Items);
    }

    [Fact]
    public async Task PlanAsync_Provision_ReturnsContextAndResolvedInputs()
    {
        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, new() { ["sku"] = "LRS" })));

        Assert.Equal(
            new RunContext("orders", _context.Deployment.ApplicationId.Value, _context.Deployment.EnvironmentId.Value),
            plan.Context);
        var input = Assert.Single(plan.Inputs);
        Assert.Equal("storage", input.Key);
        Assert.Equal($"{StorageType} template", input.TemplateName);
        Assert.Equal(StorageType, input.TemplateType);
        Assert.Equal(RunInputProvider.Terraform, input.Provider);
        Assert.Equal(new RunInputSource(new Uri("https://example.com/modules.git"), "v1.0.0", StorageType),
            input.Source);
        Assert.Equal("LRS", input.Parameters["sku"]);
    }

    [Fact]
    public async Task PlanAsync_ParametersMissing_PlansEmptyParameters()
    {
        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, null)));

        Assert.Empty(Assert.Single(plan.Inputs).Parameters);
    }

    [Fact]
    public async Task PlanAsync_Provision_RecordsResourceInstanceAndGraphNodeAsProvisioning()
    {
        await _context.PlanAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, new() { ["sku"] = "LRS" })));

        var resource = Assert.Single(_context.Resources.Items);
        Assert.Equal("orders-storage", resource.Slug);
        Assert.Equal(_context.Deployment.EnvironmentId, resource.EnvironmentId);
        Assert.Equal(_context.Application.Id, resource.ApplicationId);
        Assert.Equal(_context.Application.OrganisationId, resource.OrganisationId);
        Assert.Equal([_context.Application.Id], resource.Consumers);

        var instance = Assert.Single(_context.Instances.Items);
        Assert.Equal(resource.Id, instance.ResourceId);
        Assert.Equal(ResourceInstanceStatus.Provisioning, instance.Status);
        Assert.Equal("LRS", instance.InputParameters["sku"].GetString());

        Assert.True(Assert.Single(_context.Graphs.Items).ContainsResource(resource.Id));
        Assert.Equal([new PlannedResourceInstance(instance.Id, "storage")],
            Assert.Single(_context.Plans.Items).Instances);
    }

    [Fact]
    public async Task PlanAsync_CalledAgainWithTheSameScore_ReturnsStoredPlanWithoutWarning()
    {
        var score = Score(("storage", StorageType, new() { ["sku"] = "LRS" }));
        var (runId, first) = await _context.PlanAsync(DeploymentRunOperation.Provision, score);

        var second = await _context.CreatePlanner().PlanAsync(runId, score, CancellationToken.None);

        Assert.Equal(first.Context, second.Context);
        Assert.Equal(first.Inputs.Select(i => i.Key), second.Inputs.Select(i => i.Key));
        Assert.DoesNotContain(_context.PlannerLogger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task PlanAsync_CalledAgainWithADifferentScore_ReturnsStoredPlanWithoutRecordingAgain()
    {
        var (runId, first) = await _context.PlanAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, new() { ["sku"] = "LRS" })));
        var instance = Assert.Single(_context.Instances.Items);
        var updatedAt = instance.UpdatedAt;

        var second = await _context.CreatePlanner().PlanAsync(runId,
            Score(("storage", StorageType, new() { ["sku"] = "GRS" })), CancellationToken.None);

        Assert.Equal(first.Context, second.Context);
        Assert.Equal("LRS", Assert.Single(second.Inputs).Parameters["sku"]);
        Assert.Single(_context.Plans.Items);
        Assert.Single(_context.Resources.Items);
        Assert.Equal(updatedAt, Assert.Single(_context.Instances.Items).UpdatedAt);
        Assert.Equal("LRS", instance.InputParameters["sku"].GetString());
        Assert.Contains(_context.PlannerLogger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("different score file"));
    }

    [Fact]
    public async Task PlanAsync_Redeploy_ReusesResourceAndReconfiguresInstance()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, new() { ["sku"] = "LRS" })), RunOutcome.Succeeded);

        await _context.RunAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, new() { ["sku"] = "GRS" })), RunOutcome.Succeeded);

        Assert.Single(_context.Resources.Items);
        Assert.Single(_context.Graphs.Items);
        var instance = Assert.Single(_context.Instances.Items);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        Assert.Equal("GRS", instance.InputParameters["sku"].GetString());
    }

    [Fact]
    public async Task PlanAsync_RetryAfterFailure_ProvisionsSameInstance()
    {
        var score = Score(("storage", StorageType, null));
        await _context.RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Failed);

        await _context.RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        Assert.Equal(ResourceInstanceStatus.Active, Assert.Single(_context.Instances.Items).Status);
    }

    [Fact]
    public async Task PlanAsync_ParameterReferencesResource_AddsDependency()
    {
        await _context.PlanAsync(DeploymentRunOperation.Provision, Score(
            ("vault", KeyVaultType, null),
            ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" })));

        var storage = _context.Resources.Items.Single(r => r.Slug == "orders-storage");
        var vault = _context.Resources.Items.Single(r => r.Slug == "orders-vault");
        var graph = Assert.Single(_context.Graphs.Items);
        Assert.True(graph.HasDependencyPath(storage.Id, vault.Id));
        Assert.False(graph.HasDependencyPath(vault.Id, storage.Id));
    }

    [Fact]
    public async Task PlanAsync_ReferenceRemovedFromScore_RemovesDependency()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(
                ("vault", KeyVaultType, null),
                ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" })),
            RunOutcome.Succeeded);

        await _context.RunAsync(DeploymentRunOperation.Provision,
            Score(("vault", KeyVaultType, null), ("storage", StorageType, null)), RunOutcome.Succeeded);

        var storage = _context.Resources.Items.Single(r => r.Slug == "orders-storage");
        Assert.Equal(0, Assert.Single(_context.Graphs.Items).DependencyCount(storage.Id));
    }

    [Theory]
    [InlineData("${resources.cache.id}", "not in the score file")]
    [InlineData("${resources.storage.id}", "references itself")]
    [InlineData("${resources.vault}", "malformed")]
    [InlineData("${resources.vault.id", "malformed")]
    public async Task PlanAsync_InvalidReference_ThrowsWithoutRecording(string value, string error)
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision, Score(
                ("vault", KeyVaultType, null),
                ("storage", StorageType, new() { ["key_vault_id"] = value }))));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains(error, exception.Message);
        Assert.Contains("key_vault_id", exception.Message);
        Assert.Empty(_context.Resources.Items);
        Assert.Empty(_context.Plans.Items);
    }

    [Fact]
    public async Task PlanAsync_ReferenceBetweenProviders_Throws()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision, Score(
                ("chart", HelmType, null),
                ("storage", StorageType, new() { ["name"] = "${resources.chart.name}" }))));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("only be referenced between Terraform resources", exception.Message);
    }

    [Fact]
    public async Task PlanAsync_ReferencesFormACycle_Throws()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision, Score(
                ("vault", KeyVaultType, new() { ["storage_id"] = "${resources.storage.id}" }),
                ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" }))));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("cycle", exception.Message);
        Assert.Empty(_context.Plans.Items);
    }

    [Fact]
    public async Task PlanAsync_ReferenceInsideText_PlansParameterUnchanged()
    {
        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Provision, Score(
            ("vault", KeyVaultType, null),
            ("storage", StorageType, new() { ["name"] = "orders-${resources.vault.name}-data" })));

        Assert.Equal("orders-${resources.vault.name}-data",
            plan.Inputs.Single(i => i.Key == "storage").Parameters["name"]);
    }

    [Fact]
    public async Task PlanAsync_ScoreResourceHasId_UsesIdAsResourceName()
    {
        var scoreFile = Score(("storage", StorageType, null));
        scoreFile.Resources!["storage"] = scoreFile.Resources["storage"] with { Id = "shared-storage" };

        await _context.PlanAsync(DeploymentRunOperation.Provision, scoreFile);

        Assert.Equal("shared-storage", Assert.Single(_context.Resources.Items).Slug);
    }

    [Fact]
    public async Task PlanAsync_ResourceRecordedWithAnotherTemplate_ThrowsWithoutRecording()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);
        var resource = Assert.Single(_context.Resources.Items);
        var instance = Assert.Single(_context.Instances.Items);

        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision,
                Score(("vault", KeyVaultType, null), ("storage", KeyVaultType, null))));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("different resource template", exception.Message);
        Assert.Equal([resource], _context.Resources.Items);
        Assert.Equal(ResourceInstanceStatus.Active, Assert.Single(_context.Instances.Items).Status);
        Assert.Equal(instance.UpdatedAt, _context.Instances.Items[0].UpdatedAt);
    }

    [Fact]
    public async Task PlanAsync_TwoKeysShareAnIdWithDifferentTemplates_ThrowsWithoutRecording()
    {
        var scoreFile = Score(("storage", StorageType, null), ("vault", KeyVaultType, null));
        scoreFile.Resources!["storage"] = scoreFile.Resources["storage"] with { Id = "shared" };
        scoreFile.Resources["vault"] = scoreFile.Resources["vault"] with { Id = "shared" };

        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision, scoreFile));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Empty(_context.Resources.Items);
        Assert.Empty(_context.Instances.Items);
    }

    [Fact]
    public async Task PlanAsync_KeyRenamedWithoutPreviousKey_RecordsANewResource()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);

        await _context.PlanAsync(DeploymentRunOperation.Provision, Score(("data", StorageType, null)));

        Assert.Equal(["orders-data", "orders-storage"], _context.Resources.Items.Select(r => r.Slug).Order());
        Assert.Equal(2, _context.Instances.Items.Count);
    }

    [Fact]
    public async Task PlanAsync_KeyRenamedWithPreviousKey_RenamesTheRecordedResource()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, new() { ["sku"] = "LRS" })), RunOutcome.Succeeded);
        var resource = Assert.Single(_context.Resources.Items);
        var instance = Assert.Single(_context.Instances.Items);

        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Provision,
            WithPreviousKeys(Score(("data", StorageType, new() { ["sku"] = "GRS" })), "data", "storage"));

        Assert.Equal([resource], _context.Resources.Items);
        Assert.Equal("orders-data", resource.Slug);
        Assert.Equal("orders-data", resource.Name);
        Assert.Equal([instance], _context.Instances.Items);
        Assert.Equal(ResourceInstanceStatus.Provisioning, instance.Status);
        Assert.Equal("GRS", instance.InputParameters["sku"].GetString());
        Assert.True(Assert.Single(_context.Graphs.Items).ContainsResource(resource.Id));
        var input = Assert.Single(plan.Inputs);
        Assert.Equal("data", input.Key);
        Assert.Equal(["storage"], input.PreviousKeys!);
        Assert.Equal([new PlannedResourceInstance(instance.Id, "data")], _context.Plans.Items[^1].Instances);
    }

    [Fact]
    public async Task PlanAsync_PreviousKeyKeptAfterRename_ReusesTheRenamedResource()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);
        var renamed = WithPreviousKeys(Score(("data", StorageType, null)), "data", "old, storage");
        await _context.RunAsync(DeploymentRunOperation.Provision, renamed, RunOutcome.Succeeded);

        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Provision, renamed);

        Assert.Equal("orders-data", Assert.Single(_context.Resources.Items).Slug);
        Assert.Single(_context.Instances.Items);
        Assert.Equal(["old", "storage"], Assert.Single(plan.Inputs).PreviousKeys!);
    }

    [Fact]
    public async Task PlanAsync_KeyRenamedWithStableId_KeepsTheResourceAndPlansThePreviousKey()
    {
        var original = Score(("storage", StorageType, null));
        original.Resources!["storage"] = original.Resources["storage"] with { Id = "shared-storage" };
        await _context.RunAsync(DeploymentRunOperation.Provision, original, RunOutcome.Succeeded);
        var resource = Assert.Single(_context.Resources.Items);

        var renamed = WithPreviousKeys(Score(("data", StorageType, null)), "data", "storage");
        renamed.Resources!["data"] = renamed.Resources["data"] with { Id = "shared-storage" };
        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Provision, renamed);

        Assert.Equal([resource], _context.Resources.Items);
        Assert.Equal("shared-storage", resource.Slug);
        Assert.Single(_context.Instances.Items);
        Assert.Equal(["storage"], Assert.Single(plan.Inputs).PreviousKeys!);
    }

    [Fact]
    public async Task PlanAsync_NoPreviousKeys_PlansNoPreviousKeys()
    {
        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, null)));

        Assert.Null(Assert.Single(plan.Inputs).PreviousKeys);
    }

    [Fact]
    public async Task PlanAsync_KeyRecasedWithPreviousKey_ReusesTheRecordedResource()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(("Storage", StorageType, null)),
            RunOutcome.Succeeded);
        var resource = Assert.Single(_context.Resources.Items);

        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Provision,
            WithPreviousKeys(Score(("storage", StorageType, null)), "storage", "Storage"));

        Assert.Equal([resource], _context.Resources.Items);
        Assert.Equal(["Storage"], Assert.Single(plan.Inputs).PreviousKeys!);
    }

    [Fact]
    public async Task PlanAsync_PreviousKeyStillInScore_ThrowsWithoutRecording()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision,
                WithPreviousKeys(Score(("storage", StorageType, null), ("vault", KeyVaultType, null)), "storage",
                    "vault")));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("still a resource in the score file", exception.Message);
        Assert.Empty(_context.Resources.Items);
        Assert.Empty(_context.Plans.Items);
    }

    [Fact]
    public async Task PlanAsync_PreviousKeyListedTwice_Throws()
    {
        var scoreFile = WithPreviousKeys(
            WithPreviousKeys(Score(("data", StorageType, null), ("vault", KeyVaultType, null)), "data", "old"),
            "vault", "old");

        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision, scoreFile));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("'old' is listed by both resource 'data' and resource 'vault'", exception.Message);
    }

    [Fact]
    public async Task PlanAsync_PreviousAndCurrentResourcesBothRecorded_ThrowsWithoutRenaming()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, null), ("data", StorageType, null)), RunOutcome.Succeeded);

        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision,
                WithPreviousKeys(Score(("data", StorageType, null)), "data", "storage")));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("also exists", exception.Message);
        Assert.Equal(["orders-data", "orders-storage"], _context.Resources.Items.Select(r => r.Slug).Order());
    }

    [Fact]
    public async Task PlanAsync_PreviousKeyRecordedWithAnotherTemplate_ThrowsWithoutRenaming()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);

        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Provision,
                WithPreviousKeys(Score(("vault", KeyVaultType, null)), "vault", "storage")));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("different resource template", exception.Message);
        Assert.Equal("orders-storage", Assert.Single(_context.Resources.Items).Slug);
    }

    [Fact]
    public async Task PlanAsync_DestroyAfterKeyRenamed_RemovesTheRecordedInstance()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);

        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Destroy,
            WithPreviousKeys(Score(("data", StorageType, null)), "data", "storage"));

        var instance = Assert.Single(_context.Instances.Items);
        Assert.Equal(ResourceInstanceStatus.Removing, instance.Status);
        Assert.Equal([new PlannedResourceInstance(instance.Id, "data")], _context.Plans.Items[^1].Instances);
        Assert.Equal(["storage"], Assert.Single(plan.Inputs).PreviousKeys!);
    }

    [Fact]
    public async Task PlanAsync_Destroy_MovesRecordedInstancesToRemoving()
    {
        var score = Score(("storage", StorageType, null), ("vault", KeyVaultType, null));
        await _context.RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        await _context.PlanAsync(DeploymentRunOperation.Destroy, score);

        Assert.All(_context.Instances.Items, i => Assert.Equal(ResourceInstanceStatus.Removing, i.Status));
        Assert.Equal(["storage", "vault"],
            _context.Plans.Items[^1].Instances.Select(i => i.Key).Order());
    }

    [Fact]
    public async Task PlanAsync_DestroyAfterVersionBump_PlansRecordedVersion()
    {
        var score = Score(("storage", StorageType, null));
        await _context.RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);
        var template = _context.Template(StorageType);
        template.AddVersion(new CreateNewResourceTemplateVersionRequest
        {
            Version = "2.0.0",
            Source = new ResourceTemplateVersionSource
            {
                BaseUrl = new Uri("https://example.com/modules.git"),
                FolderPath = StorageType,
                Tag = "v2.0.0"
            },
            Notes = "Second version.",
            State = ResourceTemplateVersionState.Active
        });

        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Destroy, score);

        Assert.Equal("v1.0.0", Assert.Single(plan.Inputs).Source.Tag);
        Assert.Equal(template.Versions[0].Id, Assert.Single(_context.Instances.Items).TemplateVersionId);
    }

    [Fact]
    public async Task PlanAsync_DestroyAfterVersionDeactivated_PlansRecordedVersion()
    {
        var score = Score(("storage", StorageType, null));
        await _context.RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);
        var template = _context.Template(StorageType);
        template.DeactivateVersion(template.Versions[0].Id);

        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Destroy, score);

        Assert.Equal("v1.0.0", Assert.Single(plan.Inputs).Source.Tag);
        Assert.Equal(ResourceInstanceStatus.Removing, Assert.Single(_context.Instances.Items).Status);
    }

    [Fact]
    public async Task PlanAsync_DestroyWithDifferentTemplate_ThrowsWithoutRemoving()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);

        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            _context.PlanAsync(DeploymentRunOperation.Destroy, Score(("storage", KeyVaultType, null))));

        Assert.Equal(RunPlanFailure.Invalid, exception.Failure);
        Assert.Contains("different resource template", exception.Message);
        Assert.Equal(ResourceInstanceStatus.Active, Assert.Single(_context.Instances.Items).Status);
    }

    [Fact]
    public async Task PlanAsync_DestroyWithNothingRecorded_PlansInputsWithoutCreatingRecords()
    {
        var (_, plan) = await _context.PlanAsync(DeploymentRunOperation.Destroy,
            Score(("storage", StorageType, null)));

        Assert.Single(plan.Inputs);
        Assert.Empty(_context.Resources.Items);
        Assert.Empty(_context.Instances.Items);
    }
}
