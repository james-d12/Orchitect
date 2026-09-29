using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Infrastructure.Engine.Executor;
using Orchitect.Infrastructure.Engine.Queue;
using Orchitect.Infrastructure.Engine.Secret;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Queue;

public sealed class DeploymentQueueTests
{
    [Fact]
    public async Task WorkItem_ExecutorSucceeds_SetsDeployingThenDeployed()
    {
        var (deployment, repository, services) = Setup();
        var workItem = await QueueAsync(deployment, new FakeExecutor());

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Deployed], repository.Statuses);
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
    [InlineData(0, DeploymentStatus.Destroyed)]
    [InlineData(1, DeploymentStatus.Failed)]
    public async Task WorkItem_Destroy_SetsDestroyingThenResult(long exitCode, DeploymentStatus expected)
    {
        var (deployment, repository, services) = Setup(Deployed());
        var executor = new FakeExecutor { ExitCode = exitCode };
        var workItem = await QueueAsync(deployment, executor, operation: DeploymentOperation.Destroy);

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Destroying, expected], repository.Statuses);
        Assert.Equal(["--operation", "Destroy"], executor.Context!.Arguments.TakeLast(2));
    }

    [Fact]
    public async Task WorkItem_DestroyWhenNotDestroyable_ThrowsWithoutExecuting()
    {
        var (deployment, repository, services) = Setup();
        var executor = new FakeExecutor();
        var workItem = await QueueAsync(deployment, executor, operation: DeploymentOperation.Destroy);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workItem(services, CancellationToken.None));

        Assert.False(executor.Executed);
        Assert.Empty(repository.Statuses);
    }

    [Fact]
    public async Task WorkItem_RunnerExitsNonZero_SetsDeployingThenFailed()
    {
        var (deployment, repository, services) = Setup();
        var workItem = await QueueAsync(deployment, new FakeExecutor { ExitCode = 1 });

        await workItem(services, CancellationToken.None);

        Assert.Equal([DeploymentStatus.Deploying, DeploymentStatus.Failed], repository.Statuses);
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
    }

    private static Deployment Deployed() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId("abc123"))
            .Start()
            .ProcessDeploymentStatus(0, null);

    private static (Deployment, RecordingDeploymentRepository, IServiceProvider) Setup(Deployment? existing = null)
    {
        var deployment = existing ??
                         Deployment.Create(new ApplicationId(), new EnvironmentId(), new CommitId("abc123"));
        var repository = new RecordingDeploymentRepository { Deployment = deployment };
        var services = new ServiceCollection()
            .AddSingleton<IDeploymentRepository>(repository)
            .BuildServiceProvider();
        return (deployment, repository, services);
    }

    private static async Task<Func<IServiceProvider, CancellationToken, ValueTask>> QueueAsync(
        Deployment deployment, IExecutor executor, ExecutorOptions? options = null,
        IRunnerSecretTokenProvider? tokenProvider = null,
        DeploymentOperation operation = DeploymentOperation.Provision)
    {
        var processor = new CapturingQueueProcessor();
        var queue = new DeploymentQueue(processor, executor,
            Options.Create(options ?? new ExecutorOptions { Image = "runner:test" }),
            tokenProvider ?? new StaticTokenProvider(new Dictionary<string, string>()),
            NullLogger<DeploymentQueue>.Instance);

        await queue.QueueDeploymentTaskAsync(new DeploymentQueueRequest(deployment.ApplicationId, deployment.Id, operation));

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
        public Exception? Exception { get; init; }
        public Action? OnExecute { get; init; }
        public bool Executed { get; private set; }
        public ExecutorContext? Context { get; private set; }

        public Task<ExecutorResult> ExecuteAsync(ExecutorContext context,
            CancellationToken cancellationToken = default)
        {
            Executed = true;
            Context = context;
            OnExecute?.Invoke();
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new ExecutorResult(null, new OperationCanceledException(cancellationToken)));
            }

            return Task.FromResult(Exception is null ? new ExecutorResult(ExitCode) : new ExecutorResult(null, Exception));
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
        public List<DeploymentStatus> Statuses { get; } = [];

        public Task<Deployment?> GetByIdAsync(DeploymentId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Deployment);

        public Task<Deployment?> UpdateAsync(Deployment deployment, CancellationToken cancellationToken = default)
        {
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
}
