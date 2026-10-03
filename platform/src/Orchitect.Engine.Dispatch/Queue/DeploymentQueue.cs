using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Dispatch.Auth;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Secret;

namespace Orchitect.Engine.Dispatch.Queue;

public sealed class DeploymentQueue : IDeploymentQueue
{
    private readonly IBackgroundTaskQueueProcessor _backgroundTaskQueueProcessor;
    private readonly IExecutor _executor;
    private readonly ExecutorOptions _executorOptions;
    private readonly IRunnerSecretTokenProvider _tokenProvider;
    private readonly IDeploymentRunCancellation _cancellation;
    private readonly ILogger<DeploymentQueue> _logger;

    public DeploymentQueue(
        IBackgroundTaskQueueProcessor backgroundTaskQueueProcessor,
        IExecutor executor,
        IOptions<ExecutorOptions> executorOptions,
        IRunnerSecretTokenProvider tokenProvider,
        IDeploymentRunCancellation cancellation,
        ILogger<DeploymentQueue> logger)
    {
        _backgroundTaskQueueProcessor = backgroundTaskQueueProcessor;
        _executor = executor;
        _tokenProvider = tokenProvider;
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
            using var stop = _cancellation.Track(request.RunId);

            try
            {
                await RunAsync(deployments, runs, request, activity, stop.StopRequested, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                activity.RecordException(exception);
                await FailOwnedRunAsync(deployments, runs, request, exception);
                throw;
            }
        });
    }

    private async Task RunAsync(IDeploymentRepository deployments, IDeploymentRunRepository runs,
        DeploymentQueueRequest request, Activity? activity, CancellationToken stopRequested, CancellationToken ct)
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

        if (run.Operation == DeploymentRunOperation.Destroy)
        {
            if (deployment.Status != DeploymentStatus.Destroying)
            {
                throw new InvalidOperationException(
                    $"Deployment '{deployment.Id.Value}' must be Destroying to run a destroy, but is {deployment.Status}.");
            }
        }
        else
        {
            deployment = deployment.Start();
            await deployments.UpdateAsync(deployment, ct);
        }

        var token = RunnerToken.Generate();
        run = run.Start().IssueToken(token.Hash,
            DateTime.UtcNow + _executorOptions.Timeout + _executorOptions.StopGracePeriod);
        await runs.UpdateAsync(run, ct);

        var result = await ExecuteAsync(request, run, token, stopRequested, ct);

        if (result.Stopped && result.ExitCode != 0)
        {
            await CancelAsync(deployments, runs, deployment, run, result);
            return;
        }

        var exception = result.Exception is OperationCanceledException && !ct.IsCancellationRequested
            ? new TimeoutException(
                $"Deployment '{deployment.Id.Value}' was cancelled without the API shutting down.",
                result.Exception)
            : result.Exception;

        var processed = deployment.ProcessDeploymentStatus(result.ExitCode, exception);
        if (processed != deployment)
        {
            await deployments.UpdateAsync(processed, CancellationToken.None);
        }

        var completed = run.Complete(result.ExitCode, exception, result.RunnerId);
        if (completed != run)
        {
            await runs.UpdateAsync(completed, CancellationToken.None);
        }

        _logger.LogInformation("Deployment {DeploymentId} is {Status} after run {RunId} {RunStatus}.",
            processed.Id.Value, processed.Status, completed.Id.Value, completed.Status);
    }

    private async Task CancelAsync(IDeploymentRepository deployments, IDeploymentRunRepository runs,
        Deployment deployment, DeploymentRun run, ExecutorResult result)
    {
        var cancelled = deployment.Cancel();
        await deployments.UpdateAsync(cancelled, CancellationToken.None);

        var cancelledRun = run.Cancel(result.ExitCode, result.RunnerId);
        await runs.UpdateAsync(cancelledRun, CancellationToken.None);

        _logger.LogInformation("Deployment {DeploymentId} is {Status} after run {RunId} was cancelled.",
            cancelled.Id.Value, cancelled.Status, cancelledRun.Id.Value);
    }

    private async Task FailOwnedRunAsync(IDeploymentRepository deployments, IDeploymentRunRepository runs,
        DeploymentQueueRequest request, Exception failure)
    {
        try
        {
            var run = await runs.GetByIdAsync(request.RunId, CancellationToken.None);

            if (run is null)
            {
                return;
            }

            var deployment = await deployments.GetByIdAsync(request.DeploymentId, CancellationToken.None);
            var owned = run.Operation == DeploymentRunOperation.Destroy
                ? deployment?.Status == DeploymentStatus.Destroying
                : deployment?.Status is DeploymentStatus.Pending or DeploymentStatus.Deploying;

            if (deployment is not null && owned)
            {
                await deployments.UpdateAsync(deployment.Interrupt(failure.Message), CancellationToken.None);
                _logger.LogWarning(
                    "Deployment {DeploymentId} was {Status} when its work item failed and is now Failed.",
                    deployment.Id.Value, deployment.Status);
            }

            if (run.IsActive)
            {
                await runs.UpdateAsync(run.Interrupt(failure.Message), CancellationToken.None);
            }
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

            return await _executor.ExecuteAsync(new ExecutorContext
            {
                Image = _executorOptions.Image,
                RunId = run.Id.Value.ToString(),
                Arguments =
                [
                    RunnerArguments.ApplicationId, request.ApplicationId.Value.ToString(),
                    RunnerArguments.DeploymentId, request.DeploymentId.Value.ToString(),
                    RunnerArguments.Operation, ToRunnerOperation(run.Operation).ToString()
                ],
                Configuration = _executorOptions.ToEnvironment(),
                Secrets = new Dictionary<string, string>(_executorOptions.Configuration.Concat(tokenEnvironment))
                {
                    [RunnerEnvironment.RunToken] = token.Value
                },
                Network = _executorOptions.Network,
                DatabaseHost = _executorOptions.DatabaseHost,
                DatabasePort = _executorOptions.DatabasePort,
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

    private static RunnerOperation ToRunnerOperation(DeploymentRunOperation operation) => operation switch
    {
        DeploymentRunOperation.Provision => RunnerOperation.Provision,
        DeploymentRunOperation.Destroy => RunnerOperation.Destroy,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };
}
