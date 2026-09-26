using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Infrastructure.Engine.Executor;
using Orchitect.Infrastructure.Engine.Secret;

namespace Orchitect.Infrastructure.Engine.Queue;

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
        await _backgroundTaskQueueProcessor.QueueBackgroundWorkItemAsync(async (sp, ct) =>
        {
            var repository = sp.GetRequiredService<IDeploymentRepository>();

            var deployment = await repository.GetByIdAsync(request.DeploymentId, ct)
                             ?? throw new InvalidOperationException(
                                 $"Deployment '{request.DeploymentId.Value}' was not found.");

            deployment = deployment.Start();
            await repository.UpdateAsync(deployment, ct);

            var result = await ExecuteAsync(request, ct);

            var processed = deployment.ProcessDeploymentStatus(result.ExitCode, result.Exception);
            if (processed != deployment)
            {
                await repository.UpdateAsync(processed, CancellationToken.None);
            }

            _logger.LogInformation("Deployment {DeploymentId} is {Status}.", processed.Id.Value, processed.Status);
        });
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
                    "--application-id", request.ApplicationId.Value.ToString(),
                    "--deployment-id", request.DeploymentId.Value.ToString()
                ],
                Configuration = _executorOptions.ToEnvironment().Concat(tokenEnvironment).ToDictionary(),
                Network = _executorOptions.Network,
                DatabaseHost = _executorOptions.DatabaseHost,
                DatabasePort = _executorOptions.DatabasePort,
                MemoryBytes = _executorOptions.MemoryBytes,
                NanoCpus = _executorOptions.NanoCpus,
                PidsLimit = _executorOptions.PidsLimit,
                StopGracePeriod = _executorOptions.StopGracePeriod
            }, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not run deployment {DeploymentId}.", request.DeploymentId.Value);
            return new ExecutorResult(null, exception);
        }
    }
}
