using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Completion;

namespace Orchitect.Engine.Dispatch.Executor;

public sealed class RunnerContainerSweepService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);
    private static readonly string[] FinishedStates = ["exited", "created", "dead"];
    private const string ExitedState = "exited";

    private readonly IServiceProvider _serviceProvider;
    private readonly ExecutorOptions _options;
    private readonly ILogger<RunnerContainerSweepService> _logger;
    private readonly DateTime _startedAt = DateTime.UtcNow;

    public RunnerContainerSweepService(
        IServiceProvider serviceProvider,
        IOptions<ExecutorOptions> options,
        ILogger<RunnerContainerSweepService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to sweep leftover runner containers.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        var docker = _serviceProvider.GetRequiredService<IDockerClient>();

        var containers = await docker.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool> { [$"{RunnerContainerLabels.Runner}=true"] = true }
                }
            },
            cancellationToken);

        var unreconciled = await ReconcileDeploymentsAsync(docker, containers, cancellationToken);

        var limit = _options.Timeout + _options.StopGracePeriod;
        var overdueBefore = DateTime.UtcNow - limit;
        var removeBefore = overdueBefore > _startedAt ? overdueBefore : _startedAt;
        var removed = 0;
        var overrun = 0;

        foreach (var container in containers)
        {
            var created = container.Created.ToUniversalTime();

            if (FinishedStates.Contains(container.State, StringComparer.OrdinalIgnoreCase))
            {
                if (created < removeBefore && !unreconciled.Contains(GetRunId(container) ?? string.Empty) &&
                    await RemoveAsync(docker, container.ID, cancellationToken))
                {
                    removed++;
                }
            }
            else if (created < overdueBefore)
            {
                overrun++;
                _logger.LogWarning(
                    "Runner container {ContainerId} for run {RunId} is {State} and was created at {Created}, " +
                    "longer ago than the {Limit} timeout and grace period.",
                    container.ID, GetRunId(container),
                    container.State, created, limit);
            }
        }

        if (removed > 0 || overrun > 0)
        {
            _logger.LogInformation(
                "Runner container sweep removed {RemovedCount} containers and found {OverrunCount} still running past their timeout.",
                removed, overrun);
        }
    }

    private async Task<HashSet<string>> ReconcileDeploymentsAsync(
        IDockerClient docker,
        IList<ContainerListResponse> containers,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var deployments = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var runs = scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>();
        var completer = scope.ServiceProvider.GetRequiredService<IRunCompleter>();
        var completion = scope.ServiceProvider.GetRequiredService<IRunCompletionHandler>();
        var active = await deployments.GetActiveAsync(_startedAt, cancellationToken);
        var unreconciled = new HashSet<string>();

        foreach (var deployment in active)
        {
            var runId = deployment.Id.Value.ToString();

            try
            {
                var run = await runs.GetLatestAsync(deployment.Id, cancellationToken);
                runId = run?.Id.Value.ToString() ?? runId;
                var container = containers.Where(c => GetRunId(c) == runId).MaxBy(c => c.Created);

                var outcome = await ReconcileAsync(docker, deployment, container, cancellationToken);

                if (outcome is null)
                {
                    unreconciled.Add(runId);
                    continue;
                }

                var cancelled = run is { IsActive: true, CancelRequestedAt: not null } && outcome.ExitCode != 0;
                var result = outcome.ExitCode is { } exitCode
                    ? RunResult.FromExitCode(exitCode, container?.ID)
                    : RunResult.Failure(outcome.Reason!);

                if (cancelled)
                {
                    await runs.UpdateAsync(run!.Cancel(outcome.ExitCode, container?.ID), cancellationToken);
                    await deployments.UpdateAsync(deployment.Cancel(), cancellationToken);
                    await CompleteCancelledRunAsync(completer, run.Id, cancellationToken);
                }
                else if (run is null)
                {
                    await deployments.UpdateAsync(result.Outcome == RunOutcome.Succeeded
                        ? deployment.Succeed()
                        : deployment.Fail(result.ErrorSummary!), cancellationToken);
                }
                else
                {
                    await completion.CompleteAsync(run.Id, result, cancellationToken);
                }

                _logger.LogInformation(
                    "Deployment {DeploymentId} was left {PreviousStatus} by an earlier API process and run {RunId} was reconciled{Cancelled} from {Outcome}.",
                    deployment.Id.Value, deployment.Status, runId, cancelled ? " as cancelled" : string.Empty,
                    result.Outcome);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                unreconciled.Add(runId);
                _logger.LogWarning(exception, "Failed to reconcile deployment {DeploymentId} left {Status}.",
                    deployment.Id.Value, deployment.Status);
            }
        }

        return unreconciled;
    }

    private async Task CompleteCancelledRunAsync(IRunCompleter completer, DeploymentRunId runId,
        CancellationToken cancellationToken)
    {
        try
        {
            await completer.CompleteAsync(runId, RunOutcome.Failed, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Could not finish the instances of cancelled run {RunId}.", runId.Value);
        }
    }

    private static async Task<ReconcileOutcome?> ReconcileAsync(
        IDockerClient docker,
        Deployment deployment,
        ContainerListResponse? container,
        CancellationToken cancellationToken)
    {
        if (deployment.Status == DeploymentStatus.Pending)
        {
            return ReconcileOutcome.Interrupted("The API stopped before the deployment started.");
        }

        if (container is null)
        {
            return ReconcileOutcome.Interrupted("The runner container was not found after the API restarted.");
        }

        if (!FinishedStates.Contains(container.State, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.Equals(container.State, ExitedState, StringComparison.OrdinalIgnoreCase))
        {
            return ReconcileOutcome.Interrupted($"The runner container was {container.State} after the API restarted.");
        }

        var inspect = await docker.Containers.InspectContainerAsync(container.ID, cancellationToken);
        return inspect.State is { } state
            ? new ReconcileOutcome(state.ExitCode, null)
            : ReconcileOutcome.Interrupted("The runner container state could not be read after the API restarted.");
    }

    private sealed record ReconcileOutcome(long? ExitCode, string? Reason)
    {
        public static ReconcileOutcome Interrupted(string reason) => new(null, reason);
    }

    private static string? GetRunId(ContainerListResponse container) =>
        container.Labels is not null && container.Labels.TryGetValue(RunnerContainerLabels.RunId, out var runId)
            ? runId
            : null;

    private async Task<bool> RemoveAsync(IDockerClient docker, string containerId, CancellationToken cancellationToken)
    {
        try
        {
            await docker.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);

            _logger.LogInformation("Removed leftover runner container {ContainerId}.", containerId);
            return true;
        }
        catch (DockerContainerNotFoundException exception)
        {
            _logger.LogDebug(exception, "Leftover runner container {ContainerId} was already removed.", containerId);
            return false;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Failed to remove leftover runner container {ContainerId}.", containerId);
            return false;
        }
    }
}
