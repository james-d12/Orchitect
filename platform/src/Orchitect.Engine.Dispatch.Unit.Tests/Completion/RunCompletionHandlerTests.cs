using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Completion;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Completion;

public sealed class RunCompletionHandlerTests
{
    private static readonly Uri ModuleUrl = new("https://example.com/modules.git");

    private readonly OrganisationId _organisationId = new();
    private readonly ApplicationId _applicationId = new();
    private readonly EnvironmentId _environmentId = new(Guid.NewGuid());
    private readonly InMemoryRunRepository _runs = new();
    private readonly IDeploymentRepository _deployments = Substitute.For<IDeploymentRepository>();
    private readonly IResourceInstanceRepository _instances = Substitute.For<IResourceInstanceRepository>();
    private readonly IResourceRepository _resources = Substitute.For<IResourceRepository>();
    private readonly IResourceTemplateRepository _templates = Substitute.For<IResourceTemplateRepository>();
    private readonly IResourceDependencyGraphRepository _graphs = Substitute.For<IResourceDependencyGraphRepository>();
    private readonly ResourceTemplate _template;
    private readonly Resource _resource;
    private readonly ResourceDependencyGraph _graph;
    private readonly RunCompletionHandler _handler;

    public RunCompletionHandlerTests()
    {
        _template = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
        {
            OrganisationId = _organisationId,
            Name = "storage",
            Type = "azure-storage-account",
            Description = "A storage account.",
            Provider = ResourceTemplateProvider.Terraform,
            Version = "1.0.0",
            Source = new ResourceTemplateVersionSource { BaseUrl = ModuleUrl, FolderPath = "storage", Tag = "v1.0.0" },
            Notes = "Initial version.",
            State = ResourceTemplateVersionState.Active
        });
        _resource = Resource.Create(new CreateResourceRequest(_organisationId, "orders-storage", string.Empty,
            _template.Id, _environmentId, ResourceKind.Direct, _applicationId));
        _resource.AddConsumer(_applicationId);
        _graph = ResourceDependencyGraph.Create(_organisationId, _environmentId);
        _graph.AddResource(_resource.Id);

        _templates.GetByIdAsync(_template.Id, Arg.Any<CancellationToken>()).Returns(_template);
        _resources.GetByIdAsync(_resource.Id, Arg.Any<CancellationToken>()).Returns(_resource);
        _graphs.GetByEnvironmentAsync(_environmentId, Arg.Any<CancellationToken>()).Returns(_graph);

        _handler = new RunCompletionHandler(_runs, _deployments, _instances, _resources, _templates, _graphs,
            NullLogger<RunCompletionHandler>.Instance);
    }

    [Fact]
    public async Task CompleteAsync_ProvisionSucceeded_ActivatesInstancesAndDeploys()
    {
        var instance = Instance(ResourceInstanceStatus.Provisioning);
        var (deployment, run) = Setup(DeploymentRunOperation.Provision, instance);

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Succeeded)),
            CancellationToken.None);

        Assert.True(completed);
        Assert.Equal(DeploymentRunStatus.Succeeded, _runs.Run!.Status);
        Assert.Null(_runs.Run.TokenHash);
        Assert.Null(_runs.Run.ExitCode);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        Assert.Equal(ModuleUrl, instance.Output!.Location);
        Assert.Equal("orders", instance.Output.Workspace);
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Deployed);
    }

    [Fact]
    public async Task CompleteAsync_ProvisionFailed_FailsInstancesAndDeployment()
    {
        var instance = Instance(ResourceInstanceStatus.Provisioning);
        var (deployment, run) = Setup(DeploymentRunOperation.Provision, instance);

        await _handler.CompleteAsync(run.Id,
            RunResult.FromReport(Report(RunOutcome.Failed, "terraform apply failed")), CancellationToken.None);

        Assert.Equal(DeploymentRunStatus.Failed, _runs.Run!.Status);
        Assert.Equal("terraform apply failed", _runs.Run.ErrorSummary);
        Assert.Equal(ResourceInstanceStatus.Failed, instance.Status);
        await _deployments.Received(1).UpdateAsync(
            Arg.Is<Deployment>(d => d.Id == deployment.Id && d.Status == DeploymentStatus.Failed &&
                                    d.ErrorSummary == "terraform apply failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_FailedReportWithoutSummary_UsesDefaultSummary()
    {
        var (_, run) = Setup(DeploymentRunOperation.Provision);

        await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Failed, " ")),
            CancellationToken.None);

        Assert.Equal(RunResult.ReportedFailureSummary, _runs.Run!.ErrorSummary);
    }

    [Fact]
    public async Task CompleteAsync_DestroySucceeded_RemovesInstancesAndReleasesResources()
    {
        var instance = Instance(ResourceInstanceStatus.Removing);
        var (deployment, run) = Setup(DeploymentRunOperation.Destroy, instance);

        await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Succeeded)),
            CancellationToken.None);

        Assert.Equal(ResourceInstanceStatus.Removed, instance.Status);
        Assert.Empty(_resource.Consumers);
        Assert.False(_graph.ContainsResource(_resource.Id));
        await _resources.Received(1).UpdateAsync(_resource, Arg.Any<CancellationToken>());
        await _graphs.Received(1).UpdateAsync(_graph, Arg.Any<CancellationToken>());
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Destroyed);
    }

    [Fact]
    public async Task CompleteAsync_DestroyFailed_MarksRemovalFailedAndKeepsResources()
    {
        var instance = Instance(ResourceInstanceStatus.Removing);
        var (deployment, run) = Setup(DeploymentRunOperation.Destroy, instance);

        await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(1), CancellationToken.None);

        Assert.Equal(ResourceInstanceStatus.RemovalFailed, instance.Status);
        Assert.Equal([_applicationId], _resource.Consumers);
        Assert.True(_graph.ContainsResource(_resource.Id));
        await _graphs.DidNotReceive().UpdateAsync(Arg.Any<ResourceDependencyGraph>(), Arg.Any<CancellationToken>());
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Failed);
    }

    [Theory]
    [InlineData(0, DeploymentRunStatus.Succeeded, DeploymentStatus.Deployed)]
    [InlineData(137, DeploymentRunStatus.Failed, DeploymentStatus.Failed)]
    public async Task CompleteAsync_NoReport_ExitCodeDecides(long exitCode, DeploymentRunStatus expectedRun,
        DeploymentStatus expected)
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(exitCode, "container-1"),
            CancellationToken.None);

        Assert.True(completed);
        Assert.Equal(expectedRun, _runs.Run!.Status);
        Assert.Equal(exitCode, _runs.Run.ExitCode);
        Assert.Equal("container-1", _runs.Run.RunnerId);
        await AssertDeploymentUpdatedAsync(deployment.Id, expected);
    }

    [Fact]
    public async Task CompleteAsync_DuplicateReport_IsNoOp()
    {
        var instance = Instance(ResourceInstanceStatus.Provisioning);
        var (deployment, run) = Setup(DeploymentRunOperation.Provision, instance);
        await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Succeeded)),
            CancellationToken.None);
        _deployments.GetByIdAsync(deployment.Id, Arg.Any<CancellationToken>()).Returns(deployment.Succeed());
        var completedRun = _runs.Run;
        _instances.ClearReceivedCalls();
        _deployments.ClearReceivedCalls();

        var completed = await _handler.CompleteAsync(run.Id,
            RunResult.FromReport(Report(RunOutcome.Failed, "late")), CancellationToken.None);

        Assert.False(completed);
        Assert.Same(completedRun, _runs.Run);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        await _instances.DidNotReceive().UpdateAsync(Arg.Any<ResourceInstance>(), Arg.Any<CancellationToken>());
        await _deployments.DidNotReceive().UpdateAsync(Arg.Any<Deployment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_ExitCodeAfterReport_OnlyRecordsExitCode()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);
        await _handler.CompleteAsync(run.Id,
            RunResult.FromReport(Report(RunOutcome.Failed, "terraform apply failed")), CancellationToken.None);
        _deployments.GetByIdAsync(deployment.Id, Arg.Any<CancellationToken>())
            .Returns(deployment.Fail("terraform apply failed"));
        _deployments.ClearReceivedCalls();

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(0, "container-1"),
            CancellationToken.None);

        Assert.False(completed);
        Assert.Equal(DeploymentRunStatus.Failed, _runs.Run!.Status);
        Assert.Equal("terraform apply failed", _runs.Run.ErrorSummary);
        Assert.Equal(0, _runs.Run.ExitCode);
        Assert.Equal("container-1", _runs.Run.RunnerId);
        await _deployments.DidNotReceive().UpdateAsync(Arg.Any<Deployment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_AnotherCallerFinishedTheRunFirst_DoesNotSettleInstances()
    {
        var instance = Instance(ResourceInstanceStatus.Provisioning);
        var (_, run) = Setup(DeploymentRunOperation.Provision, instance);
        _runs.FinishedElsewhere = run.Fail("terraform apply failed");

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(0, "container-1"),
            CancellationToken.None);

        Assert.False(completed);
        Assert.Equal(DeploymentRunStatus.Failed, _runs.Run!.Status);
        Assert.Equal(0, _runs.Run.ExitCode);
        Assert.Equal(ResourceInstanceStatus.Provisioning, instance.Status);
    }

    [Fact]
    public async Task CompleteAsync_FinishedRunWithDeploymentLeftActive_FinishesDeployment()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);
        _runs.Run = run.Succeed(0);

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(0), CancellationToken.None);

        Assert.False(completed);
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Deployed);
    }

    [Fact]
    public async Task CompleteAsync_FinishedRunThatIsNoLongerLatest_LeavesDeployment()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Destroy);
        _runs.Run = run.Fail("terraform destroy failed");
        _runs.Latest = DeploymentRun.Queue(deployment.Id, DeploymentRunOperation.Destroy).Start();

        await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Succeeded)),
            CancellationToken.None);

        await _deployments.DidNotReceive().UpdateAsync(Arg.Any<Deployment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_InstanceNotInFlight_IsLeftAlone()
    {
        var instance = Instance(ResourceInstanceStatus.Provisioning);
        instance.Transition(ResourceInstanceStatus.Failed);
        var (_, run) = Setup(DeploymentRunOperation.Provision, instance);

        await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Succeeded)),
            CancellationToken.None);

        Assert.Equal(ResourceInstanceStatus.Failed, instance.Status);
        await _instances.DidNotReceive().UpdateAsync(Arg.Any<ResourceInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_QueuedRunFailed_FailsPendingDeployment()
    {
        var deployment = Deployment.Create(_applicationId, _environmentId, new CommitId(new string('a', 40)),
            "test@example.com");
        var run = DeploymentRun.Queue(deployment.Id, DeploymentRunOperation.Provision);
        _runs.Run = run;
        _deployments.GetByIdAsync(deployment.Id, Arg.Any<CancellationToken>()).Returns(deployment);

        await _handler.CompleteAsync(run.Id, RunResult.Failure("The API stopped before the deployment started."),
            CancellationToken.None);

        Assert.Equal(DeploymentRunStatus.Failed, _runs.Run.Status);
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Failed);
    }

    [Fact]
    public async Task CompleteAsync_RunMissing_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.CompleteAsync(new DeploymentRunId(), RunResult.FromExitCode(0), CancellationToken.None));
    }

    private (Deployment, DeploymentRun) Setup(DeploymentRunOperation operation, params ResourceInstance[] instances)
    {
        var deployment = Deployment.Create(_applicationId, _environmentId, new CommitId(new string('a', 40)),
            "test@example.com").Start();

        if (operation == DeploymentRunOperation.Destroy)
        {
            deployment = deployment.Succeed().StartDestroy();
        }

        var run = DeploymentRun.Queue(deployment.Id, operation).Start()
            .IssueToken("token-hash", DateTime.UtcNow.AddHours(1))
            .RecordPlan("orders", instances.Select(i => i.Id).ToList());
        _runs.Run = run;
        _deployments.GetByIdAsync(deployment.Id, Arg.Any<CancellationToken>()).Returns(deployment);

        foreach (var instance in instances)
        {
            _instances.GetByIdAsync(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        }

        return (deployment, run);
    }

    private ResourceInstance Instance(ResourceInstanceStatus status)
    {
        var instance = ResourceInstance.Create(new CreateResourceInstanceRequest(_resource.Id, _organisationId,
            "orders-storage-instance", _template.GetLatestVersion()!.Id, _environmentId, null));

        if (status == ResourceInstanceStatus.Removing)
        {
            instance.Transition(ResourceInstanceStatus.PendingRemoval);
        }

        instance.Transition(status);
        return instance;
    }

    private static RunCompletion Report(RunOutcome outcome, string? errorSummary = null) => new(outcome, errorSummary);

    private Task<Deployment?> AssertDeploymentUpdatedAsync(DeploymentId id, DeploymentStatus status) =>
        _deployments.Received(1).UpdateAsync(Arg.Is<Deployment>(d => d.Id == id && d.Status == status),
            Arg.Any<CancellationToken>());

    private sealed class InMemoryRunRepository : IDeploymentRunRepository
    {
        public DeploymentRun? Run { get; set; }
        public DeploymentRun? Latest { get; set; }
        public DeploymentRun? FinishedElsewhere { get; set; }

        public Task<DeploymentRun?> GetByIdAsync(DeploymentRunId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Run?.Id == id ? Run : null);

        public Task<DeploymentRun?> GetLatestAsync(DeploymentId deploymentId,
            CancellationToken cancellationToken = default) => Task.FromResult(Latest ?? Run);

        public Task<DeploymentRun?> UpdateAsync(DeploymentRun run, CancellationToken cancellationToken = default)
        {
            Run = run;
            return Task.FromResult<DeploymentRun?>(run);
        }

        public Task<bool> TryFinishAsync(DeploymentRun run, CancellationToken cancellationToken = default)
        {
            if (FinishedElsewhere is not null)
            {
                Run = FinishedElsewhere;
                return Task.FromResult(false);
            }

            if (Run is not { IsActive: true })
            {
                return Task.FromResult(false);
            }

            Run = run;
            return Task.FromResult(true);
        }

        public Task<DeploymentRun?> CreateAsync(DeploymentRun run, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IEnumerable<DeploymentRun> GetAll() => throw new NotSupportedException();

        public Task<DeploymentRun?> GetByTokenHashAsync(string tokenHash,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
