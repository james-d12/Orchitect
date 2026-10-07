using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Auth;
using Orchitect.Engine.Dispatch.Completion;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Secret;
using Orchitect.Engine.Dispatch.Storage;

namespace Orchitect.Engine.Dispatch.Queue;

public sealed class DeploymentQueue : IDeploymentQueue
{
    private readonly IBackgroundTaskQueueProcessor _backgroundTaskQueueProcessor;
    private readonly IExecutor _executor;
    private readonly ExecutorOptions _executorOptions;
    private readonly IRunnerSecretTokenProvider _tokenProvider;
    private readonly IRunnerStorageTokenProvider _storageTokenProvider;
    private readonly IDeploymentRunCancellation _cancellation;
    private readonly ILogger<DeploymentQueue> _logger;

    public DeploymentQueue(
        IBackgroundTaskQueueProcessor backgroundTaskQueueProcessor,
        IExecutor executor,
        IOptions<ExecutorOptions> executorOptions,
        IRunnerSecretTokenProvider tokenProvider,
        IRunnerStorageTokenProvider storageTokenProvider,
        IDeploymentRunCancellation cancellation,
        ILogger<DeploymentQueue> logger)
    {
        _backgroundTaskQueueProcessor = backgroundTaskQueueProcessor;
        _executor = executor;
        _tokenProvider = tokenProvider;
        _storageTokenProvider = storageTokenProvider;
        _cancellation = cancellation;
        _logger = logger;
        _executorOptions = executorOptions.Value;
    }

    public async Task QueueDeploymentTaskAsync(DeploymentQueueRequest request, CancellationToken token = default)
    {
        var parentContext = Activity.Current?.Context ?? default;

        await _backgroundTaskQueueProcessor.QueueBackgroundWorkItemAsync(async (sp, ct) =>
        {
            using var activity = Tracing.StartActivity(parentContext, nameof(QueueDeploymentTaskAsync));
            activity?.SetTag("orchitect.deployment.id", request.DeploymentId.Value);
            activity?.SetTag("orchitect.run.id", request.RunId.Value);

            var deployments = sp.GetRequiredService<IDeploymentRepository>();
            var runs = sp.GetRequiredService<IDeploymentRunRepository>();
            var completer = sp.GetRequiredService<IRunCompleter>();
            var completion = sp.GetRequiredService<IRunCompletionHandler>();
            using var stop = _cancellation.Track(request.RunId);

            try
            {
                await RunAsync(deployments, runs, completer, completion, request, activity, stop.StopRequested,
                    ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                activity.RecordException(exception);
                await FailRunAsync(completion, request, exception);
                throw;
            }
        });
    }

    private async Task RunAsync(IDeploymentRepository deployments, IDeploymentRunRepository runs,
        IRunCompleter completer, IRunCompletionHandler completion, DeploymentQueueRequest request, Activity? activity,
        CancellationToken stopRequested, CancellationToken ct)
    {
        var deployment = await deployments.GetByIdAsync(request.DeploymentId, ct)
                         ?? throw new InvalidOperationException(
                             $"Deployment '{request.DeploymentId.Value}' was not found.");
        var run = await runs.GetByIdAsync(request.RunId, ct)
                  ?? throw new InvalidOperationException($"Run '{request.RunId.Value}' was not found.");

        activity?.SetTag("orchitect.run.operation", run.Operation.ToString());

        if (!run.IsActive)
        {
            _logger.LogInformation("Run {RunId} of deployment {DeploymentId} is {RunStatus} and will not be started.",
                run.Id.Value, deployment.Id.Value, run.Status);
            return;
        }

        if (run.Operation == DeploymentRunOperation.Destroy && deployment.Status != DeploymentStatus.Destroying)
        {
            throw new InvalidOperationException(
                $"Deployment '{deployment.Id.Value}' must be Destroying to run a destroy, but is {deployment.Status}.");
        }

        var token = RunnerToken.Generate();
        var claimed = await ClaimAsync(runs, run.Start().IssueToken(token.Hash,
            DateTime.UtcNow + _executorOptions.Timeout + _executorOptions.StopGracePeriod), ct);

        if (claimed is null)
        {
            return;
        }

        run = claimed;

        if (run.Operation == DeploymentRunOperation.Provision)
        {
            deployment = deployment.Start();
            await deployments.UpdateAsync(deployment, ct);
        }

        var result = await ExecuteAsync(request, run, token, stopRequested, ct);

        if (result.Exception is OperationCanceledException && ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Run {RunId} of deployment {DeploymentId} was left running because the API is shutting down.",
                run.Id.Value, deployment.Id.Value);
            return;
        }

        if (result.Stopped && result.ExitCode != 0 &&
            (await runs.GetByIdAsync(run.Id, CancellationToken.None))?.IsActive == true)
        {
            await CancelAsync(deployments, runs, completer, deployment, run, result);
            return;
        }

        var runResult = result switch
        {
            { Exception: OperationCanceledException } => RunResult.Failure(
                $"Deployment '{deployment.Id.Value}' was cancelled without the API shutting down.",
                result.ExitCode, result.RunnerId),
            { Exception: { } exception } => RunResult.Failure(exception.Message, result.ExitCode, result.RunnerId),
            { ExitCode: { } exitCode } => RunResult.FromExitCode(exitCode, result.RunnerId),
            _ => throw new InvalidOperationException("A run result needs an exit code or an exception.")
        };

        var completed = await completion.CompleteAsync(run.Id, runResult, CancellationToken.None);

        _logger.LogInformation(
            "Run {RunId} of deployment {DeploymentId} exited with {ExitCode}; {Source} decided its outcome.",
            run.Id.Value, deployment.Id.Value, result.ExitCode, completed ? "the exit code" : "the runner's report");
    }

    private async Task CancelAsync(IDeploymentRepository deployments, IDeploymentRunRepository runs,
        IRunCompleter completer, Deployment deployment, DeploymentRun run, ExecutorResult result)
    {
        var cancelled = deployment.Cancel();
        await deployments.UpdateAsync(cancelled, CancellationToken.None);

        var cancelledRun = await UpdateRunAsync(runs, run, r => r.Cancel(result.ExitCode, result.RunnerId));

        _logger.LogInformation("Deployment {DeploymentId} is {Status} after run {RunId} was cancelled.",
            cancelled.Id.Value, cancelled.Status, cancelledRun.Id.Value);

        try
        {
            await completer.CompleteAsync(cancelledRun.Id, RunOutcome.Failed, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not finish the instances of cancelled run {RunId}.",
                cancelledRun.Id.Value);
        }
    }

    private async Task<DeploymentRun?> ClaimAsync(IDeploymentRunRepository runs, DeploymentRun started,
        CancellationToken ct)
    {
        try
        {
            return await runs.UpdateAsync(started, ct) ?? started;
        }
        catch (DeploymentRunConflictException exception)
        {
            var current = await runs.GetByIdAsync(started.Id, ct);

            if (current is { IsActive: true })
            {
                throw new InvalidOperationException(
                    $"Run '{started.Id.Value}' was changed while it was being started.", exception);
            }

            _logger.LogInformation("Run {RunId} became {RunStatus} before it started.",
                started.Id.Value, current?.Status);
            return null;
        }
    }

    private static async Task<DeploymentRun> UpdateRunAsync(IDeploymentRunRepository runs, DeploymentRun run,
        Func<DeploymentRun, DeploymentRun> transition)
    {
        try
        {
            return await SaveTransitionAsync(runs, run, transition);
        }
        catch (DeploymentRunConflictException)
        {
            var current = await runs.GetByIdAsync(run.Id, CancellationToken.None)
                          ?? throw new InvalidOperationException($"Run '{run.Id.Value}' was not found.");
            return await SaveTransitionAsync(runs, current, transition);
        }
    }

    private static async Task<DeploymentRun> SaveTransitionAsync(IDeploymentRunRepository runs, DeploymentRun run,
        Func<DeploymentRun, DeploymentRun> transition)
    {
        var next = transition(run);
        return next == run ? run : await runs.UpdateAsync(next, CancellationToken.None) ?? next;
    }

    private async Task FailRunAsync(IRunCompletionHandler completion, DeploymentQueueRequest request,
        Exception failure)
    {
        try
        {
            await completion.CompleteAsync(request.RunId, RunResult.Failure(failure.Message),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Could not fail run {RunId} of deployment {DeploymentId} after its work item failed. It is reconciled on the next API start.",
                request.RunId.Value, request.DeploymentId.Value);
        }
    }

    private async Task<ExecutorResult> ExecuteAsync(DeploymentQueueRequest request, DeploymentRun run,
        RunnerToken token, CancellationToken stopRequested, CancellationToken ct)
    {
        try
        {
            var tokenEnvironment = await _tokenProvider.GetEnvironmentAsync(_executorOptions.SecretProvider, ct);
            var storageTokenEnvironment =
                await _storageTokenProvider.GetEnvironmentAsync(_executorOptions.Storage, ct);

            return await _executor.ExecuteAsync(new ExecutorContext
            {
                Image = _executorOptions.Image,
                RunId = run.Id.Value.ToString(),
                Arguments = [RunnerArguments.RunId, run.Id.Value.ToString()],
                Configuration = _executorOptions.ToEnvironment(),
                Secrets = new Dictionary<string, string>(_executorOptions.Configuration
                    .Concat(tokenEnvironment)
                    .Concat(storageTokenEnvironment))
                {
                    [RunnerEnvironment.RunToken] = token.Value
                },
                Binds = _executorOptions.ToBinds(),
                Network = _executorOptions.Network,
                ApiBaseUrl = _executorOptions.ApiBaseUrl,
                MemoryBytes = _executorOptions.MemoryBytes,
                NanoCpus = _executorOptions.NanoCpus,
                PidsLimit = _executorOptions.PidsLimit,
                StopGracePeriod = _executorOptions.StopGracePeriod,
                Timeout = _executorOptions.Timeout,
                StopRequested = stopRequested
            }, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not run deployment {DeploymentId} in run {RunId}.",
                request.DeploymentId.Value, run.Id.Value);
            return new ExecutorResult(null, exception);
        }
    }
}
