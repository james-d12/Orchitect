using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Secret;

namespace Orchitect.Engine.Dispatch.Queue;

public sealed class DeploymentQueue : IDeploymentQueue
{
    private readonly IBackgroundTaskQueueProcessor _backgroundTaskQueueProcessor;
    private readonly IExecutor _executor;
    private readonly ExecutorOptions _executorOptions;
    private readonly IRunnerSecretTokenProvider _tokenProvider;
    private readonly ILogger<DeploymentQueue> _logger;

    public DeploymentQueue(
        IBackgroundTaskQueueProcessor backgroundTaskQueueProcessor,
        IExecutor executor,
        IOptions<ExecutorOptions> executorOptions,
        IRunnerSecretTokenProvider tokenProvider,
        ILogger<DeploymentQueue> logger)
    {
        _backgroundTaskQueueProcessor = backgroundTaskQueueProcessor;
        _executor = executor;
        _tokenProvider = tokenProvider;
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
            activity?.SetTag("orchitect.run.operation", request.Operation.ToString());

            var repository = sp.GetRequiredService<IDeploymentRepository>();

            try
            {
                await RunAsync(repository, request, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                activity.RecordException(exception);
                await FailOwnedDeploymentAsync(repository, request);
                throw;
            }
        });
    }

    private async Task RunAsync(IDeploymentRepository repository, DeploymentQueueRequest request,
        CancellationToken ct)
    {
        var deployment = await repository.GetByIdAsync(request.DeploymentId, ct)
                         ?? throw new InvalidOperationException(
                             $"Deployment '{request.DeploymentId.Value}' was not found.");

        if (request.Operation == RunnerOperation.Destroy)
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
            await repository.UpdateAsync(deployment, ct);
        }

        var result = await ExecuteAsync(request, ct);
        var exception = result.Exception is OperationCanceledException && !ct.IsCancellationRequested
            ? new TimeoutException(
                $"Deployment '{deployment.Id.Value}' was cancelled without the API shutting down.",
                result.Exception)
            : result.Exception;

        var processed = deployment.ProcessDeploymentStatus(result.ExitCode, exception);
        if (processed != deployment)
        {
            await repository.UpdateAsync(processed, CancellationToken.None);
        }

        _logger.LogInformation("Deployment {DeploymentId} is {Status}.", processed.Id.Value, processed.Status);
    }

    private async Task FailOwnedDeploymentAsync(IDeploymentRepository repository, DeploymentQueueRequest request)
    {
        try
        {
            var deployment = await repository.GetByIdAsync(request.DeploymentId, CancellationToken.None);
            var owned = request.Operation == RunnerOperation.Destroy
                ? deployment?.Status == DeploymentStatus.Destroying
                : deployment?.Status is DeploymentStatus.Pending or DeploymentStatus.Deploying;

            if (deployment is null || !owned)
            {
                return;
            }

            await repository.UpdateAsync(deployment.Interrupt(), CancellationToken.None);
            _logger.LogWarning("Deployment {DeploymentId} was {Status} when its work item failed and is now Failed.",
                deployment.Id.Value, deployment.Status);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Could not fail deployment {DeploymentId} after its work item failed. It is reconciled on the next API start.",
                request.DeploymentId.Value);
        }
    }

    private async Task<ExecutorResult> ExecuteAsync(DeploymentQueueRequest request, CancellationToken ct)
    {
        try
        {
            var tokenEnvironment = await _tokenProvider.GetEnvironmentAsync(_executorOptions.SecretProvider, ct);

            return await _executor.ExecuteAsync(new ExecutorContext
            {
                Image = _executorOptions.Image,
                RunId = request.DeploymentId.Value.ToString(),
                Arguments =
                [
                    RunnerArguments.ApplicationId, request.ApplicationId.Value.ToString(),
                    RunnerArguments.DeploymentId, request.DeploymentId.Value.ToString(),
                    RunnerArguments.Operation, request.Operation.ToString()
                ],
                Configuration = _executorOptions.ToEnvironment(),
                Secrets = _executorOptions.Configuration.Concat(tokenEnvironment).ToDictionary(),
                Network = _executorOptions.Network,
                DatabaseHost = _executorOptions.DatabaseHost,
                DatabasePort = _executorOptions.DatabasePort,
                MemoryBytes = _executorOptions.MemoryBytes,
                NanoCpus = _executorOptions.NanoCpus,
                PidsLimit = _executorOptions.PidsLimit,
                StopGracePeriod = _executorOptions.StopGracePeriod,
                Timeout = _executorOptions.Timeout
            }, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not run deployment {DeploymentId}.", request.DeploymentId.Value);
            return new ExecutorResult(null, exception);
        }
    }
}
