using Microsoft.Extensions.Options;
using Orchitect.Infrastructure.Engine.Executor;
using Orchitect.Infrastructure.Engine.Secret;

namespace Orchitect.Infrastructure.Engine.Queue;

public sealed class DeploymentQueue : IDeploymentQueue
{
    private readonly IBackgroundTaskQueueProcessor _backgroundTaskQueueProcessor;
    private readonly IExecutor _executor;
    private readonly ExecutorOptions _executorOptions;
    private readonly IRunnerSecretTokenProvider _tokenProvider;

    public DeploymentQueue(
        IBackgroundTaskQueueProcessor backgroundTaskQueueProcessor, 
        IExecutor executor, 
        IOptions<ExecutorOptions> executorOptions,
        IRunnerSecretTokenProvider tokenProvider)
    {
        _backgroundTaskQueueProcessor = backgroundTaskQueueProcessor;
        _executor = executor;
        _tokenProvider = tokenProvider;
        _executorOptions = executorOptions.Value;
    }

    public async Task QueueDeploymentTaskAsync(DeploymentQueueRequest request, CancellationToken token = default)
    {
        await _backgroundTaskQueueProcessor.QueueBackgroundWorkItemAsync(async (sp, ct) =>
        {
            var tokenEnvironment = await _tokenProvider.GetEnvironmentAsync(_executorOptions.SecretProvider, ct);

            await _executor.ExecuteAsync(new ExecutorContext
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
                DatabasePort = _executorOptions.DatabasePort
            }, ct);
        });
    }
}