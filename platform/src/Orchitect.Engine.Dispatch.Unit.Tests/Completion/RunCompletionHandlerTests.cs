using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchitect.Domain.Core;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Completion;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Completion;

public sealed class RunCompletionHandlerTests
{
    private readonly InMemoryRunRepository _runs = new();
    private readonly IDeploymentRepository _deployments = Substitute.For<IDeploymentRepository>();
    private readonly IRunCompleter _completer = Substitute.For<IRunCompleter>();
    private readonly RunCompletionHandler _handler;

    public RunCompletionHandlerTests()
    {
        _handler = new RunCompletionHandler(new PassThroughUnitOfWork(), _runs, _deployments, _completer,
            NullLogger<RunCompletionHandler>.Instance);
    }

    [Theory]
    [InlineData(0, DeploymentRunStatus.Succeeded, DeploymentStatus.Deployed, RunOutcome.Succeeded)]
    [InlineData(137, DeploymentRunStatus.Failed, DeploymentStatus.Failed, RunOutcome.Failed)]
    public async Task CompleteAsync_NoReport_ExitCodeDecides(long exitCode, DeploymentRunStatus expectedRun,
        DeploymentStatus expected, RunOutcome expectedOutcome)
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(exitCode, "container-1"),
            CancellationToken.None);

        Assert.True(completed);
        Assert.Equal(expectedRun, _runs.Run!.Status);
        Assert.Equal(exitCode, _runs.Run.ExitCode);
        Assert.Equal("container-1", _runs.Run.RunnerId);
        Assert.Null(_runs.Run.TokenHash);
        await AssertDeploymentUpdatedAsync(deployment.Id, expected);
        await _completer.Received(1).CompleteAsync(run.Id, expectedOutcome, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_ReportSucceeded_FinishesRunAndDeploymentAndSettlesInstances()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);

        await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Succeeded)),
            CancellationToken.None);

        Assert.Equal(DeploymentRunStatus.Succeeded, _runs.Run!.Status);
        Assert.Null(_runs.Run.ExitCode);
        Assert.Null(_runs.Run.TokenHash);
        Assert.True(_runs.LockedBeforeRead);
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Deployed);
        await _completer.Received(1).CompleteAsync(run.Id, RunOutcome.Succeeded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_ReportFailed_FailsDeploymentWithTheSummary()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);

        await _handler.CompleteAsync(run.Id,
            RunResult.FromReport(Report(RunOutcome.Failed, "terraform apply failed")), CancellationToken.None);

        Assert.Equal(DeploymentRunStatus.Failed, _runs.Run!.Status);
        Assert.Equal("terraform apply failed", _runs.Run.ErrorSummary);
        await _deployments.Received(1).UpdateAsync(
            Arg.Is<Deployment>(d => d.Id == deployment.Id && d.Status == DeploymentStatus.Failed &&
                                    d.ErrorSummary == "terraform apply failed"),
            Arg.Any<CancellationToken>());
        await _completer.Received(1).CompleteAsync(run.Id, RunOutcome.Failed, Arg.Any<CancellationToken>());
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
    public async Task CompleteAsync_DestroySucceeded_DestroysDeployment()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Destroy);

        await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Succeeded)),
            CancellationToken.None);

        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Destroyed);
        await _completer.Received(1).CompleteAsync(run.Id, RunOutcome.Succeeded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_DuplicateReport_IsNoOp()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);
        await _handler.CompleteAsync(run.Id, RunResult.FromReport(Report(RunOutcome.Succeeded)),
            CancellationToken.None);
        _deployments.GetByIdAsync(deployment.Id, Arg.Any<CancellationToken>()).Returns(deployment.Succeed());
        var completedRun = _runs.Run;
        _deployments.ClearReceivedCalls();

        var completed = await _handler.CompleteAsync(run.Id,
            RunResult.FromReport(Report(RunOutcome.Failed, "late")), CancellationToken.None);

        Assert.False(completed);
        Assert.Same(completedRun, _runs.Run);
        await _deployments.DidNotReceive().UpdateAsync(Arg.Any<Deployment>(), Arg.Any<CancellationToken>());
        await _completer.DidNotReceive().CompleteAsync(run.Id, RunOutcome.Failed, Arg.Any<CancellationToken>());
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
        await _completer.DidNotReceive().CompleteAsync(run.Id, RunOutcome.Succeeded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_FinishedRunWithDeploymentLeftActive_FinishesDeployment()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);
        _runs.Run = run.Succeed(0);

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(0), CancellationToken.None);

        Assert.False(completed);
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Deployed);
        await _completer.Received(1).CompleteAsync(run.Id, RunOutcome.Succeeded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_FinishedRunThatIsNoLongerLatest_LeavesDeploymentAndInstances()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Destroy);
        _runs.Run = run.Fail("terraform destroy failed");
        _runs.Latest = DeploymentRun.Queue(deployment.Id, DeploymentRunOperation.Destroy).Start();

        await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(1), CancellationToken.None);

        await _deployments.DidNotReceive().UpdateAsync(Arg.Any<Deployment>(), Arg.Any<CancellationToken>());
        await _completer.DidNotReceiveWithAnyArgs().CompleteAsync(default, default, default);
    }

    [Fact]
    public async Task CompleteAsync_CancelledRun_OnlyRecordsExitCode()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);
        _runs.Run = run.Cancel();
        _deployments.GetByIdAsync(deployment.Id, Arg.Any<CancellationToken>()).Returns(deployment.Cancel());

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(143), CancellationToken.None);

        Assert.False(completed);
        Assert.Equal(DeploymentRunStatus.Cancelled, _runs.Run.Status);
        Assert.Equal(143, _runs.Run.ExitCode);
        await _deployments.DidNotReceive().UpdateAsync(Arg.Any<Deployment>(), Arg.Any<CancellationToken>());
        await _completer.DidNotReceiveWithAnyArgs().CompleteAsync(default, default, default);
    }

    [Fact]
    public async Task CompleteAsync_QueuedRunFailed_FailsPendingDeployment()
    {
        var deployment = NewDeployment();
        var run = DeploymentRun.Queue(deployment.Id, DeploymentRunOperation.Provision);
        _runs.Run = run;
        _deployments.GetByIdAsync(deployment.Id, Arg.Any<CancellationToken>()).Returns(deployment);

        await _handler.CompleteAsync(run.Id, RunResult.Failure("The API stopped before the deployment started."),
            CancellationToken.None);

        Assert.Equal(DeploymentRunStatus.Failed, _runs.Run.Status);
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Failed);
    }

    [Fact]
    public async Task CompleteAsync_SettlingFails_KeepsTheFinishedRunAndThrows()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);
        _completer.CompleteAsync(Arg.Any<DeploymentRunId>(), Arg.Any<RunOutcome>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database unavailable."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.CompleteAsync(run.Id, RunResult.FromExitCode(0), CancellationToken.None));

        Assert.Equal(DeploymentRunStatus.Succeeded, _runs.Run!.Status);
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Deployed);
    }

    [Fact]
    public async Task CompleteAsync_RunChangedConcurrently_RetriesWithTheFreshRun()
    {
        var (deployment, run) = Setup(DeploymentRunOperation.Provision);
        var requestedAt = DateTime.UtcNow;
        _runs.ConflictWith = current => current.RequestCancel(requestedAt);

        var completed = await _handler.CompleteAsync(run.Id, RunResult.FromExitCode(0), CancellationToken.None);

        Assert.True(completed);
        Assert.Equal(DeploymentRunStatus.Succeeded, _runs.Run!.Status);
        Assert.Equal(requestedAt, _runs.Run.CancelRequestedAt);
        await AssertDeploymentUpdatedAsync(deployment.Id, DeploymentStatus.Deployed);
    }

    [Fact]
    public async Task CompleteAsync_RunMissing_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.CompleteAsync(new DeploymentRunId(), RunResult.FromExitCode(0), CancellationToken.None));
    }

    private (Deployment, DeploymentRun) Setup(DeploymentRunOperation operation)
    {
        var deployment = NewDeployment().Start();

        if (operation == DeploymentRunOperation.Destroy)
        {
            deployment = deployment.Succeed().StartDestroy();
        }

        var run = DeploymentRun.Queue(deployment.Id, operation).Start()
            .IssueToken("token-hash", DateTime.UtcNow.AddHours(1));
        _runs.Run = run;
        _deployments.GetByIdAsync(deployment.Id, Arg.Any<CancellationToken>()).Returns(deployment);

        return (deployment, run);
    }

    private static Deployment NewDeployment() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId(new string('a', 40)),
            "test@example.com");

    private static RunCompletion Report(RunOutcome outcome, string? errorSummary = null) => new(outcome, errorSummary);

    private Task<Deployment?> AssertDeploymentUpdatedAsync(DeploymentId id, DeploymentStatus status) =>
        _deployments.Received(1).UpdateAsync(Arg.Is<Deployment>(d => d.Id == id && d.Status == status),
            Arg.Any<CancellationToken>());

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
            CancellationToken cancellationToken = default) => work(cancellationToken);

        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            work(cancellationToken);
    }

    private sealed class InMemoryRunRepository : IDeploymentRunRepository
    {
        private bool _locked;

        public DeploymentRun? Run { get; set; }
        public DeploymentRun? Latest { get; set; }
        public bool LockedBeforeRead { get; private set; }

        public Task LockAsync(DeploymentRunId id, CancellationToken cancellationToken = default)
        {
            _locked = true;
            return Task.CompletedTask;
        }

        public Task<DeploymentRun?> GetByIdAsync(DeploymentRunId id, CancellationToken cancellationToken = default)
        {
            LockedBeforeRead = _locked;
            return Task.FromResult(Run?.Id == id ? Run : null);
        }

        public Task<DeploymentRun?> GetLatestAsync(DeploymentId deploymentId,
            CancellationToken cancellationToken = default) => Task.FromResult(Latest ?? Run);

        public Func<DeploymentRun, DeploymentRun>? ConflictWith { get; set; }

        public Task<DeploymentRun?> UpdateAsync(DeploymentRun run, CancellationToken cancellationToken = default)
        {
            if (ConflictWith is { } change)
            {
                ConflictWith = null;
                Run = change(Run!);
                throw new DeploymentRunConflictException(run.Id);
            }

            Run = run;
            return Task.FromResult<DeploymentRun?>(run);
        }

        public Task<DeploymentRun?> CreateAsync(DeploymentRun run, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IEnumerable<DeploymentRun> GetAll() => throw new NotSupportedException();

        public Task<DeploymentRun?> GetByTokenHashAsync(string tokenHash,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
