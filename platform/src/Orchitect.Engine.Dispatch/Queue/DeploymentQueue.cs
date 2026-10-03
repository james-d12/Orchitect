using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Dispatch.Auth;
using Orchitect.Engine.Dispatch.Completion;
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
            activity?.SetTag("orchitect.run.id", request.RunId.Value);

            var deployments = sp.GetRequiredService<IDeploymentRepository>();
            var runs = sp.GetRequiredService<IDeploymentRunRepository>();
            var completion = sp.GetRequiredService<IRunCompletionHandler>();

            try
            {
                await RunAsync(deployments, runs, completion, request, activity, ct);
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
        IRunCompletionHandler completion, DeploymentQueueRequest request, Activity? activity, CancellationToken ct)
    {
        var deployment = await deployments.GetByIdAsync(request.DeploymentId, ct)
                         ?? throw new InvalidOperationException(
                             $"Deployment '{request.DeploymentId.Value}' was not found.");
        var run = await runs.GetByIdAsync(request.RunId, ct)
                  ?? throw new InvalidOperationException($"Run '{request.RunId.Value}' was not found.");

        activity?.SetTag("orchitect.run.operation", run.Operation.ToString());

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

        var result = await ExecuteAsync(request, run, token, ct);

        if (result.Exception is OperationCanceledException && ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Run {RunId} of deployment {DeploymentId} was left running because the API is shutting down.",
                run.Id.Value, deployment.Id.Value);
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
        RunnerToken token, CancellationToken ct)
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
                Timeout = _executorOptions.Timeout
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
