using System.Buffers.Text;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Secret;
using Orchitect.Engine.Dispatch.Auth;
using Orchitect.Engine.Dispatch.Completion;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Queue;
using Orchitect.Engine.Dispatch.Secret;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Queue;

public sealed class DeploymentQueueTests
{
    private readonly RecordingDeploymentRunRepository _runs = new();
    private readonly DeploymentRunCancellation _cancellation = new();
    private readonly RecordingRunCompleter _completer = new();

    [Fact]
    public async Task WorkItem_ExecutorSucceeds_SetsDeployingThenDeployed()
    {
        var (deployment, repository, services) = Setup();
        var workItem = await QueueAsync(deployment, new FakeExecutor());

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Deployed], repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running, DeploymentRunStatus.Succeeded], _runs.Statuses);
        Assert.Equal(0, _runs.Run!.ExitCode);
        Assert.NotNull(_runs.Run.StartedAt);
        Assert.NotNull(_runs.Run.FinishedAt);
    }

    [Fact]
    public async Task WorkItem_PassesRunIdToExecutorAndRecordsRunnerId()
    {
        var (deployment, _, services) = Setup();
        var executor = new FakeExecutor { RunnerId = "container-1" };
        var workItem = await QueueAsync(deployment, executor);

        await workItem(services, CancellationToken.None);

        Assert.Equal(_runs.Run!.Id.Value.ToString(), executor.Context!.RunId);
        Assert.NotEqual(deployment.Id.Value.ToString(), executor.Context.RunId);
        Assert.Equal("container-1", _runs.Run.RunnerId);
    }

    [Fact]
    public async Task WorkItem_Provision_PassesProvisionOperation()
    {
        var (deployment, _, services) = Setup();
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor);

        await workItem(services, CancellationToken.None);

        Assert.Equal(["--operation", "Provision"], executor.Context!.Arguments.TakeLast(2));
    }

    [Theory]
    [InlineData(0, DeploymentStatus.Destroyed, DeploymentRunStatus.Succeeded)]
    [InlineData(1, DeploymentStatus.Failed, DeploymentRunStatus.Failed)]
    public async Task WorkItem_Destroy_SetsResult(long exitCode, DeploymentStatus expected,
        DeploymentRunStatus expectedRun)
    {
        var (deployment, repository, services) = Setup(Deployed().StartDestroy(), DeploymentRunOperation.Destroy);
        var executor = new FakeExecutor { ExitCode = exitCode };
        var workItem = await QueueAsync(deployment, executor);

        await workItem(services, CancellationToken.None);

        Assert.Equal([expected], repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running, expectedRun], _runs.Statuses);
        Assert.Equal(["--operation", "Destroy"], executor.Context!.Arguments.TakeLast(2));
    }

    [Fact]
    public async Task WorkItem_DestroyWhenNotDestroying_ThrowsWithoutExecuting()
    {
        var (deployment, repository, services) = Setup(Deployed(), DeploymentRunOperation.Destroy);
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workItem(services, CancellationToken.None));

        Assert.False(executor.Executed);
        Assert.Empty(repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Failed], _runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_RunnerExitsNonZero_SetsDeployingThenFailed()
    {
        var (deployment, repository, services) = Setup();
        var workItem = await QueueAsync(deployment, new FakeExecutor { ExitCode = 1 });

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Failed], repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running, DeploymentRunStatus.Failed], _runs.Statuses);
        Assert.Equal(1, _runs.Run!.ExitCode);
        Assert.Equal("The runner exited with code 1.", _runs.Run.ErrorSummary);
    }

    [Fact]
    public async Task WorkItem_ExecutorReturnsException_SetsDeployingThenFailed()
    {
        var (deployment, repository, services) = Setup();
        var workItem = await QueueAsync(deployment,
            new FakeExecutor { Exception = new InvalidOperationException("boom") });

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Failed], repository.Statuses);
    }

    [Fact]
    public async Task WorkItem_CancelledOnShutdown_LeavesDeploying()
    {
        var (deployment, repository, services) = Setup();
        using var cancellation = new CancellationTokenSource();
        var workItem = await QueueAsync(deployment, new FakeExecutor { OnExecute = cancellation.Cancel });

        await workItem(services, cancellation.Token);

        Assert.Equal([DeploymentStatus.Deploying], repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running], _runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_CancelledWithoutShutdown_SetsDeployingThenFailed()
    {
        var (deployment, repository, services) = Setup();
        var workItem = await QueueAsync(deployment,
            new FakeExecutor { Exception = new TaskCanceledException("HTTP request timed out.") });

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Failed], repository.Statuses);
    }

    [Fact]
    public async Task WorkItem_FailsBeforeRun_FailsDeploymentAndRethrows()
    {
        var (deployment, repository, services) = Setup();
        repository.FailingUpdates = 1;
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workItem(services, CancellationToken.None));

        Assert.False(executor.Executed);
        Assert.Equal([DeploymentStatus.Failed], repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running, DeploymentRunStatus.Failed], _runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_FailsOnShutdown_LeavesDeploymentForReconcile()
    {
        var (deployment, repository, services) = Setup();
        repository.FailingUpdates = 1;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var workItem = await QueueAsync(deployment, new FakeExecutor());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workItem(services, cancellation.Token));

        Assert.Empty(repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running], _runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_PassesTokenAndConfigurationAsSecrets()
    {
        var (deployment, _, services) = Setup();
        var executor = new FakeExecutor();
        var options = new ExecutorOptions
        {
            Image = "runner:test",
            Configuration = new Dictionary<string, string> { ["ARM_CLIENT_SECRET"] = "s3cr3t" }
        };
        var tokenProvider = new StaticTokenProvider(new Dictionary<string, string> { ["TOKEN"] = "t0ken" });
        var workItem = await QueueAsync(deployment, executor, options, tokenProvider);

        await workItem(services, CancellationToken.None);

        Assert.Equal("s3cr3t", executor.Context!.Secrets["ARM_CLIENT_SECRET"]);
        Assert.Equal("t0ken", executor.Context.Secrets["TOKEN"]);
        Assert.DoesNotContain("ARM_CLIENT_SECRET", executor.Context.Configuration.Keys);
        Assert.DoesNotContain("TOKEN", executor.Context.Configuration.Keys);
    }

    [Fact]
    public async Task WorkItem_IssuesRunTokenAndStoresOnlyItsHash()
    {
        var (deployment, _, services) = Setup();
        var executor = new FakeExecutor();
        var options = new ExecutorOptions
        {
            Image = "runner:test",
            Timeout = TimeSpan.FromMinutes(30),
            StopGracePeriod = TimeSpan.FromMinutes(5)
        };
        var before = DateTime.UtcNow;
        var workItem = await QueueAsync(deployment, executor, options);

        await workItem(services, CancellationToken.None);

        var token = executor.Context!.Secrets[RunnerEnvironment.RunToken];
        var running = _runs.Updates[0];
        Assert.Equal(DeploymentRunStatus.Running, running.Status);
        Assert.Equal(RunnerToken.ComputeHash(token), running.TokenHash);
        Assert.NotEqual(token, running.TokenHash);
        Assert.Equal(32, Base64Url.DecodeFromChars(token).Length);
        Assert.InRange(running.TokenExpiresAt!.Value, before.AddMinutes(35), DateTime.UtcNow.AddMinutes(35));
        Assert.DoesNotContain(RunnerEnvironment.RunToken, executor.Context.Configuration.Keys);
        Assert.DoesNotContain(executor.Context.Arguments, a => a.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WorkItem_EachRunGetsItsOwnToken()
    {
        var (first, _, firstServices) = Setup();
        var firstExecutor = new FakeExecutor();
        await (await QueueAsync(first, firstExecutor))(firstServices, CancellationToken.None);
        var (second, _, secondServices) = Setup();
        var secondExecutor = new FakeExecutor();
        await (await QueueAsync(second, secondExecutor))(secondServices, CancellationToken.None);

        Assert.NotEqual(firstExecutor.Context!.Secrets[RunnerEnvironment.RunToken],
            secondExecutor.Context!.Secrets[RunnerEnvironment.RunToken]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task WorkItem_RunnerExits_RevokesToken(long exitCode)
    {
        var (deployment, _, services) = Setup();
        var workItem = await QueueAsync(deployment, new FakeExecutor { ExitCode = exitCode });

        await workItem(services, CancellationToken.None);

        Assert.False(_runs.Run!.IsActive);
        Assert.Null(_runs.Run.TokenHash);
        Assert.Null(_runs.Run.TokenExpiresAt);
    }

    [Fact]
    public async Task WorkItem_ExecutorReturnsException_RevokesToken()
    {
        var (deployment, _, services) = Setup();
        var workItem = await QueueAsync(deployment,
            new FakeExecutor { Exception = new InvalidOperationException("boom") });

        await workItem(services, CancellationToken.None);

        Assert.Null(_runs.Run!.TokenHash);
    }

    [Fact]
    public async Task WorkItem_CancelledOnShutdown_KeepsTokenForTheStillRunningRunner()
    {
        var (deployment, _, services) = Setup();
        using var cancellation = new CancellationTokenSource();
        var workItem = await QueueAsync(deployment, new FakeExecutor { OnExecute = cancellation.Cancel });

        await workItem(services, cancellation.Token);

        Assert.NotNull(_runs.Run!.TokenHash);
    }

    [Fact]
    public async Task WorkItem_RunCancelledWhileQueued_DoesNotExecute()
    {
        var (deployment, repository, services) = Setup();
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor);
        _runs.Run = _runs.Run!.Cancel();

        await workItem(services, CancellationToken.None);

        Assert.False(executor.Executed);
        Assert.Empty(repository.Statuses);
        Assert.Empty(_runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_RunCancelledWhileClaiming_DoesNotExecuteOrStartDeployment()
    {
        var (deployment, repository, services) = Setup();
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor);
        _runs.Conflicts = 1;
        _runs.ConcurrentChange = run => run.Cancel();

        await workItem(services, CancellationToken.None);

        Assert.False(executor.Executed);
        Assert.Empty(repository.Statuses);
        Assert.Equal(DeploymentRunStatus.Cancelled, _runs.Run!.Status);
    }

    [Fact]
    public async Task WorkItem_RunChangedWhileClaimingButStillQueued_FailsRun()
    {
        var (deployment, _, services) = Setup();
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor);
        _runs.Conflicts = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workItem(services, CancellationToken.None));

        Assert.False(executor.Executed);
        Assert.Equal(DeploymentRunStatus.Failed, _runs.Run!.Status);
    }

    [Fact]
    public async Task WorkItem_CancelRequestedButRunnerSucceeded_CompletesFreshRunAsSucceeded()
    {
        var (deployment, repository, services) = Setup();
        var requestedAt = DateTime.UtcNow;
        var workItem = await QueueAsync(deployment, new FakeExecutor
        {
            OnExecute = () =>
            {
                _runs.Conflicts = 1;
                _runs.ConcurrentChange = run => run.RequestCancel(requestedAt);
            }
        });

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Deployed], repository.Statuses);
        Assert.Equal(DeploymentRunStatus.Succeeded, _runs.Run!.Status);
        Assert.Equal(requestedAt, _runs.Run.CancelRequestedAt);
    }

    [Fact]
    public async Task WorkItem_StopRequestedWhileRunning_CancelsDeploymentAndRun()
    {
        var (deployment, repository, services) = Setup();
        var executor = new FakeExecutor
        {
            RunnerId = "container-1",
            OnExecute = () => Assert.True(_cancellation.RequestStop(_runs.Run!.Id))
        };
        var workItem = await QueueAsync(deployment, executor);

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Cancelled], repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running, DeploymentRunStatus.Cancelled], _runs.Statuses);
        Assert.Equal(143, _runs.Run!.ExitCode);
        Assert.Equal("container-1", _runs.Run.RunnerId);
        Assert.NotNull(_runs.Run.FinishedAt);
        Assert.Null(_runs.Run.TokenHash);
        Assert.Equal([(_runs.Run.Id, RunOutcome.Failed)], _completer.Calls);
    }

    [Fact]
    public async Task WorkItem_StopRequestedAndCompleterFails_StillCancels()
    {
        var (deployment, repository, services) = Setup();
        _completer.Fail = true;
        var workItem = await QueueAsync(deployment,
            new FakeExecutor { OnExecute = () => _cancellation.RequestStop(_runs.Run!.Id) });

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Cancelled], repository.Statuses);
        Assert.Equal(DeploymentRunStatus.Cancelled, _runs.Run!.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task WorkItem_RunnerExitsWithoutCancel_LeavesInstancesToTheRunner(long exitCode)
    {
        var (deployment, _, services) = Setup();
        var workItem = await QueueAsync(deployment, new FakeExecutor { ExitCode = exitCode });

        await workItem(services, CancellationToken.None);

        Assert.Empty(_completer.Calls);
    }

    [Fact]
    public async Task WorkItem_DestroyStopRequested_CancelsDeploymentAndRun()
    {
        var (deployment, repository, services) = Setup(Deployed().StartDestroy(), DeploymentRunOperation.Destroy);
        var workItem = await QueueAsync(deployment,
            new FakeExecutor { OnExecute = () => _cancellation.RequestStop(_runs.Run!.Id) });

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Cancelled], repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running, DeploymentRunStatus.Cancelled], _runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_StopRequestedAfterRunnerSucceeded_KeepsDeployed()
    {
        var (deployment, repository, services) = Setup();
        var workItem = await QueueAsync(deployment, new FakeExecutor
        {
            StoppedExitCode = 0,
            OnExecute = () => _cancellation.RequestStop(_runs.Run!.Id)
        });

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Deployed], repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Running, DeploymentRunStatus.Succeeded], _runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_Finished_StopsTrackingRun()
    {
        var (deployment, _, services) = Setup();
        var workItem = await QueueAsync(deployment, new FakeExecutor());

        await workItem(services, CancellationToken.None);

        Assert.False(_cancellation.RequestStop(_runs.Run!.Id));
    }

    [Fact]
    public async Task WorkItem_DeploymentMissing_ThrowsWithoutExecuting()
    {
        var (deployment, repository, services) = Setup();
        repository.Deployment = null;
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workItem(services, CancellationToken.None));

        Assert.False(executor.Executed);
        Assert.Empty(repository.Statuses);
        Assert.Equal([DeploymentRunStatus.Failed], _runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_RunMissing_ThrowsWithoutExecuting()
    {
        var (deployment, repository, services) = Setup();
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor);
        _runs.Run = null;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workItem(services, CancellationToken.None));

        Assert.False(executor.Executed);
        Assert.Empty(repository.Statuses);
        Assert.Empty(_runs.Statuses);
    }

    [Fact]
    public async Task WorkItem_RunsInTraceOfQueueingRequest()
    {
        using var listener = new ActivityListener();
        listener.ShouldListenTo = _ => true;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);
        var (deployment, _, services) = Setup();
        var executor = new FakeExecutor();
        var request = new Activity("request").Start();
        var workItem = await QueueAsync(deployment, executor);
        request.Stop();

        await workItem(services, CancellationToken.None);

        Assert.Equal(request.TraceId, executor.TraceId);
    }

    private static Deployment Deployed() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId(new string('a', 40)),
                "test@example.com")
            .Start()
            .ProcessDeploymentStatus(0, null);

    private (Deployment, RecordingDeploymentRepository, IServiceProvider) Setup(Deployment? existing = null,
        DeploymentRunOperation operation = DeploymentRunOperation.Provision)
    {
        var deployment = existing ??
                         Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId(new string('a', 40)),
                             "test@example.com");
        var repository = new RecordingDeploymentRepository { Deployment = deployment };
        _runs.Run = DeploymentRun.Queue(deployment.Id, operation);
        var services = new ServiceCollection()
            .AddSingleton<IDeploymentRepository>(repository)
            .AddSingleton<IDeploymentRunRepository>(_runs)
            .AddSingleton<IRunCompleter>(_completer)
            .BuildServiceProvider();
        return (deployment, repository, services);
    }

    private async Task<Func<IServiceProvider, CancellationToken, ValueTask>> QueueAsync(
        Deployment deployment, IExecutor executor, ExecutorOptions? options = null,
        IRunnerSecretTokenProvider? tokenProvider = null)
    {
        var processor = new CapturingQueueProcessor();
        var queue = new DeploymentQueue(processor, executor,
            Options.Create(options ?? new ExecutorOptions { Image = "runner:test" }),
            tokenProvider ?? new StaticTokenProvider(new Dictionary<string, string>()),
            _cancellation,
            NullLogger<DeploymentQueue>.Instance);

        await queue.QueueDeploymentTaskAsync(
            new DeploymentQueueRequest(_runs.Run!.Id, deployment.ApplicationId, deployment.Id));

        return processor.WorkItem!;
    }

    private sealed class CapturingQueueProcessor : IBackgroundTaskQueueProcessor
    {
        public Func<IServiceProvider, CancellationToken, ValueTask>? WorkItem { get; private set; }

        public ValueTask QueueBackgroundWorkItemAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem)
        {
            WorkItem = workItem;
            return ValueTask.CompletedTask;
        }

        public ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeExecutor : IExecutor
    {
        public long ExitCode { get; init; }
        public long? StoppedExitCode { get; init; } = 143;
        public string? RunnerId { get; init; }
        public Exception? Exception { get; init; }
        public Action? OnExecute { get; init; }
        public bool Executed { get; private set; }
        public ExecutorContext? Context { get; private set; }
        public ActivityTraceId? TraceId { get; private set; }

        public Task<ExecutorResult> ExecuteAsync(ExecutorContext context,
            CancellationToken cancellationToken = default)
        {
            Executed = true;
            Context = context;
            TraceId = Activity.Current?.TraceId;
            OnExecute?.Invoke();
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutorResult(null, new OperationCanceledException(cancellationToken)));
            }

            if (context.StopRequested.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutorResult(StoppedExitCode, RunnerId: RunnerId, Stopped: true));
            }

            return Task.FromResult(Exception is null ? new ExecutorResult(ExitCode, RunnerId: RunnerId) : new ExecutorResult(null, Exception));
        }

        public Task<bool> SignalStopAsync(string runId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRunCompleter : IRunCompleter
    {
        public List<(DeploymentRunId, RunOutcome)> Calls { get; } = [];
        public bool Fail { get; set; }

        public Task CompleteAsync(DeploymentRunId runId, RunOutcome outcome, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("Database unavailable.");
            }

            Calls.Add((runId, outcome));
            return Task.CompletedTask;
        }
    }

    private sealed class StaticTokenProvider(IReadOnlyDictionary<string, string> environment)
        : IRunnerSecretTokenProvider
    {
        public Task<IReadOnlyDictionary<string, string>> GetEnvironmentAsync(SecretProviderOptions options,
            CancellationToken cancellationToken = default) => Task.FromResult(environment);
    }

    private sealed class RecordingDeploymentRepository : IDeploymentRepository
    {
        public Deployment? Deployment { get; set; }
        public int FailingUpdates { get; set; }
        public List<DeploymentStatus> Statuses { get; } = [];

        public Task<Deployment?> GetByIdAsync(DeploymentId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Deployment);

        public Task<Deployment?> UpdateAsync(Deployment deployment, CancellationToken cancellationToken = default)
        {
            if (FailingUpdates > 0)
            {
                FailingUpdates--;
                throw new InvalidOperationException("Database unavailable.");
            }

            Statuses.Add(deployment.Status);
            Deployment = deployment;
            return Task.FromResult<Deployment?>(deployment);
        }

        public Task<Deployment?> CreateAsync(Deployment environment, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IEnumerable<Deployment> GetAll() => throw new NotSupportedException();

        public Task<Deployment?> GetLatestAsync(ApplicationId applicationId, EnvironmentId environmentId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Deployment>> GetActiveAsync(DateTime updatedBefore,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingDeploymentRunRepository : IDeploymentRunRepository
    {
        public DeploymentRun? Run { get; set; }
        public List<DeploymentRunStatus> Statuses { get; } = [];
        public List<DeploymentRun> Updates { get; } = [];
        public int Conflicts { get; set; }
        public Func<DeploymentRun, DeploymentRun> ConcurrentChange { get; set; } = run => run;

        public Task<DeploymentRun?> GetByIdAsync(DeploymentRunId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Run?.Id == id ? Run : null);

        public Task<DeploymentRun?> UpdateAsync(DeploymentRun run, CancellationToken cancellationToken = default)
        {
            if (Conflicts > 0)
            {
                Conflicts--;
                Run = ConcurrentChange(Run!);
                throw new DeploymentRunConflictException(run.Id);
            }

            Statuses.Add(run.Status);
            Updates.Add(run);
            Run = run;
            return Task.FromResult<DeploymentRun?>(run);
        }

        public Task<DeploymentRun?> CreateAsync(DeploymentRun run, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IEnumerable<DeploymentRun> GetAll() => throw new NotSupportedException();

        public Task<DeploymentRun?> GetLatestAsync(DeploymentId deploymentId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DeploymentRun?> GetByTokenHashAsync(string tokenHash,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task LockAsync(DeploymentRunId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
