using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Engine.Dispatch.Executor;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Executor;

public sealed class RunnerContainerSweepServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromHours(1);
    private static readonly TimeSpan StopGracePeriod = TimeSpan.FromMinutes(6);

    private readonly IContainerOperations _containers = Substitute.For<IContainerOperations>();
    private readonly IDeploymentRepository _deployments = Substitute.For<IDeploymentRepository>();
    private readonly RunnerContainerSweepService _service;

    public RunnerContainerSweepServiceTests()
    {
        var docker = Substitute.For<IDockerClient>();
        docker.Containers.Returns(_containers);

        _service = new RunnerContainerSweepService(
            new ServiceCollection().AddSingleton(docker).AddSingleton(_deployments).BuildServiceProvider(),
            Options.Create(new ExecutorOptions
            {
                Image = "orchitect-runner:test",
                Timeout = Timeout,
                StopGracePeriod = StopGracePeriod
            }),
            NullLogger<RunnerContainerSweepService>.Instance);

        SetActiveDeployments();
    }

    [Fact]
    public async Task SweepAsync_ReadsDeploymentsLeftActiveBeforeStartup()
    {
        var before = DateTime.UtcNow;
        SetContainers();

        await _service.SweepAsync(CancellationToken.None);

        await _deployments.Received(1).GetActiveAsync(Arg.Is<DateTime>(d => d <= before), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_PendingDeploymentFromEarlierProcess_IsFailed()
    {
        var deployment = NewDeployment();
        SetActiveDeployments(deployment);
        SetContainers();

        await _service.SweepAsync(CancellationToken.None);

        await AssertUpdatedAsync(deployment.Id, DeploymentStatus.Failed);
    }

    [Theory]
    [InlineData(0, DeploymentStatus.Deployed)]
    [InlineData(1, DeploymentStatus.Failed)]
    public async Task SweepAsync_DeployingWithExitedContainer_AppliesExitCodeThenRemovesContainer(long exitCode,
        DeploymentStatus expected)
    {
        var deployment = NewDeployment().Start();
        SetActiveDeployments(deployment);
        SetContainers(Container("old", "exited", DateTime.UtcNow.AddMinutes(-1), deployment.Id));
        _containers.InspectContainerAsync("old", Arg.Any<CancellationToken>())
            .Returns(new ContainerInspectResponse { State = new State { ExitCode = exitCode } });

        await _service.SweepAsync(CancellationToken.None);

        await AssertUpdatedAsync(deployment.Id, expected);
        await AssertRemovedAsync("old");
    }

    [Fact]
    public async Task SweepAsync_DestroyingWithExitedContainer_BecomesDestroyed()
    {
        var deployment = NewDeployment().Start().ProcessDeploymentStatus(0, null).StartDestroy();
        SetActiveDeployments(deployment);
        SetContainers(Container("old", "exited", DateTime.UtcNow.AddMinutes(-1), deployment.Id));
        _containers.InspectContainerAsync("old", Arg.Any<CancellationToken>())
            .Returns(new ContainerInspectResponse { State = new State { ExitCode = 0 } });

        await _service.SweepAsync(CancellationToken.None);

        await AssertUpdatedAsync(deployment.Id, DeploymentStatus.Destroyed);
    }

    [Theory]
    [InlineData("created")]
    [InlineData("dead")]
    public async Task SweepAsync_DeployingWithContainerThatNeverExitedCleanly_IsFailed(string state)
    {
        var deployment = NewDeployment().Start();
        SetActiveDeployments(deployment);
        SetContainers(Container("old", state, DateTime.UtcNow.AddMinutes(-1), deployment.Id));

        await _service.SweepAsync(CancellationToken.None);

        await AssertUpdatedAsync(deployment.Id, DeploymentStatus.Failed);
        await _containers.DidNotReceive().InspectContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_DeployingWithoutContainer_IsFailed()
    {
        var deployment = NewDeployment().Start();
        SetActiveDeployments(deployment);
        SetContainers();

        await _service.SweepAsync(CancellationToken.None);

        await AssertUpdatedAsync(deployment.Id, DeploymentStatus.Failed);
    }

    [Fact]
    public async Task SweepAsync_DeployingWithRunningContainer_IsLeftActive()
    {
        var deployment = NewDeployment().Start();
        SetActiveDeployments(deployment);
        SetContainers(Container("running", "running", DateTime.UtcNow.AddMinutes(-1), deployment.Id));

        await _service.SweepAsync(CancellationToken.None);

        await _deployments.DidNotReceive().UpdateAsync(Arg.Any<Deployment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_ReconcileFails_KeepsContainerAndContinues()
    {
        var failing = NewDeployment().Start();
        var pending = NewDeployment();
        SetActiveDeployments(failing, pending);
        SetContainers(Container("old", "exited", DateTime.UtcNow.AddMinutes(-1), failing.Id));
        _containers.InspectContainerAsync("old", Arg.Any<CancellationToken>())
            .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.InternalServerError, "boom"));

        await _service.SweepAsync(CancellationToken.None);

        await AssertNotRemovedAsync();
        await AssertUpdatedAsync(pending.Id, DeploymentStatus.Failed);
    }

    [Theory]
    [InlineData("exited")]
    [InlineData("created")]
    [InlineData("dead")]
    public async Task SweepAsync_FinishedContainerFromPreviousProcess_IsRemoved(string state)
    {
        SetContainers(Container("old", state, DateTime.UtcNow.AddMinutes(-1)));

        await _service.SweepAsync(CancellationToken.None);

        await AssertRemovedAsync("old");
    }

    [Theory]
    [InlineData("exited")]
    [InlineData("created")]
    public async Task SweepAsync_FinishedContainerFromThisProcess_IsKept(string state)
    {
        SetContainers(Container("new", state, DateTime.UtcNow.AddMinutes(1)));

        await _service.SweepAsync(CancellationToken.None);

        await AssertNotRemovedAsync();
    }

    [Fact]
    public async Task SweepAsync_FinishedContainerOlderThanLimit_IsRemoved()
    {
        SetContainers(Container("overdue", "exited", DateTime.UtcNow - Timeout - StopGracePeriod - TimeSpan.FromMinutes(1)));

        await _service.SweepAsync(CancellationToken.None);

        await AssertRemovedAsync("overdue");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-600)]
    public async Task SweepAsync_RunningContainer_IsNeverRemoved(int createdMinutesAgo)
    {
        SetContainers(Container("running", "running", DateTime.UtcNow.AddMinutes(createdMinutesAgo)));

        await _service.SweepAsync(CancellationToken.None);

        await AssertNotRemovedAsync();
    }

    [Fact]
    public async Task SweepAsync_ListsAllContainersWithRunnerLabel()
    {
        SetContainers();

        await _service.SweepAsync(CancellationToken.None);

        await _containers.Received(1).ListContainersAsync(
            Arg.Is<ContainersListParameters>(p =>
                p.All == true &&
                p.Filters != null && p.Filters["label"].Count == 1 &&
                p.Filters["label"]["orchitect.runner=true"]),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepAsync_RemoveFails_ContinuesWithOtherContainers()
    {
        var created = DateTime.UtcNow.AddMinutes(-1);
        SetContainers(Container("gone", "exited", created), Container("broken", "exited", created),
            Container("old", "exited", created));
        _containers.RemoveContainerAsync("gone", Arg.Any<ContainerRemoveParameters>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "gone"));
        _containers.RemoveContainerAsync("broken", Arg.Any<ContainerRemoveParameters>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.InternalServerError, "boom"));

        await _service.SweepAsync(CancellationToken.None);

        await AssertRemovedAsync("old");
    }

    private static ContainerListResponse Container(string id, string state, DateTime created,
        DeploymentId? runId = null) => new()
    {
        ID = id,
        State = state,
        Created = created,
        Labels = new Dictionary<string, string>
        {
            ["orchitect.runner"] = "true",
            ["orchitect.run-id"] = runId?.Value.ToString() ?? "run-1"
        }
    };

    private static Deployment NewDeployment() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId("abc123"));

    private void SetActiveDeployments(params Deployment[] deployments) =>
        _deployments.GetActiveAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(deployments);

    private Task<Deployment?> AssertUpdatedAsync(DeploymentId id, DeploymentStatus status) =>
        _deployments.Received(1).UpdateAsync(Arg.Is<Deployment>(d => d.Id == id && d.Status == status),
            Arg.Any<CancellationToken>());

    private void SetContainers(params ContainerListResponse[] containers) =>
        _containers.ListContainersAsync(Arg.Any<ContainersListParameters>(), Arg.Any<CancellationToken>())
            .Returns(containers);

    private Task AssertRemovedAsync(string containerId) =>
        _containers.Received(1).RemoveContainerAsync(containerId,
            Arg.Is<ContainerRemoveParameters>(p => p.Force == true), Arg.Any<CancellationToken>());

    private Task AssertNotRemovedAsync() =>
        _containers.DidNotReceive().RemoveContainerAsync(Arg.Any<string>(), Arg.Any<ContainerRemoveParameters>(),
            Arg.Any<CancellationToken>());
}
