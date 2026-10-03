using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Unit.Tests.Plan;
using static Orchitect.Engine.Dispatch.Unit.Tests.Plan.RunTestContext;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Completion;

public sealed class RunCompleterTests
{
    private readonly RunTestContext _context = new();

    [Fact]
    public async Task CompleteAsync_ProvisionSucceeds_MarksInstancesActiveWithOutput()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);

        var instance = Assert.Single(_context.Instances.Items);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        Assert.Equal("orders", instance.Output!.Workspace);
        Assert.Equal(new Uri("https://example.com/modules.git"), instance.Output.Location);
    }

    [Fact]
    public async Task CompleteAsync_ProvisionFails_MarksInstancesFailed()
    {
        await _context.RunAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, null), ("vault", KeyVaultType, null)), RunOutcome.Failed);

        Assert.Equal(2, _context.Instances.Items.Count);
        Assert.All(_context.Instances.Items, i => Assert.Equal(ResourceInstanceStatus.Failed, i.Status));
    }

    [Fact]
    public async Task CompleteAsync_CalledAgain_LeavesFinishedInstancesAlone()
    {
        var runId = await _context.RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);

        await _context.CreateCompleter().CompleteAsync(runId, RunOutcome.Failed, CancellationToken.None);

        Assert.Equal(ResourceInstanceStatus.Active, Assert.Single(_context.Instances.Items).Status);
    }

    [Fact]
    public async Task CompleteAsync_RunNotPlanned_DoesNothing()
    {
        var run = _context.AddRun(DeploymentRun.Queue(_context.Deployment.Id, DeploymentRunOperation.Provision)
            .Start());

        await _context.CreateCompleter().CompleteAsync(run.Id, RunOutcome.Failed, CancellationToken.None);

        Assert.Empty(_context.Instances.Items);
        Assert.Empty(_context.Resources.Items);
    }

    [Fact]
    public async Task CompleteAsync_DestroySucceeds_MarksInstancesRemovedAndReleasesResources()
    {
        var score = Score(
            ("vault", KeyVaultType, null),
            ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" }));
        await _context.RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        await _context.RunAsync(DeploymentRunOperation.Destroy, score, RunOutcome.Succeeded);

        var graph = Assert.Single(_context.Graphs.Items);
        Assert.All(_context.Instances.Items, i => Assert.Equal(ResourceInstanceStatus.Removed, i.Status));
        Assert.Equal(2, _context.Resources.Items.Count);
        Assert.All(_context.Resources.Items, r => Assert.Empty(r.Consumers));
        Assert.All(_context.Resources.Items, r => Assert.False(graph.ContainsResource(r.Id)));
    }

    [Fact]
    public async Task CompleteAsync_DestroyAfterFailedProvision_MarksInstancesRemoved()
    {
        var score = Score(("storage", StorageType, null));
        await _context.RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Failed);

        await _context.RunAsync(DeploymentRunOperation.Destroy, score, RunOutcome.Succeeded);

        Assert.Equal(ResourceInstanceStatus.Removed, Assert.Single(_context.Instances.Items).Status);
    }

    [Fact]
    public async Task CompleteAsync_DestroyFails_MarksRemovalFailedAndKeepsConsumerAndGraphNode()
    {
        var score = Score(("storage", StorageType, null));
        await _context.RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        await _context.RunAsync(DeploymentRunOperation.Destroy, score, RunOutcome.Failed);

        var resource = Assert.Single(_context.Resources.Items);
        Assert.Equal(ResourceInstanceStatus.RemovalFailed, Assert.Single(_context.Instances.Items).Status);
        Assert.Equal([_context.Application.Id], resource.Consumers);
        Assert.True(Assert.Single(_context.Graphs.Items).ContainsResource(resource.Id));
    }
}
