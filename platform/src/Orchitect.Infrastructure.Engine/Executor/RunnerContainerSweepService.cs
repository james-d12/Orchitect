using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Infrastructure.Engine.Executor;

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
        var repository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var deployments = await repository.GetActiveAsync(_startedAt, cancellationToken);
        var unreconciled = new HashSet<string>();

        foreach (var deployment in deployments)
        {
            var runId = deployment.Id.Value.ToString();
            var container = containers.Where(c => GetRunId(c) == runId).MaxBy(c => c.Created);

            try
            {
                var reconciled = await ReconcileAsync(docker, deployment, container, cancellationToken);

                if (reconciled is null)
                {
                    unreconciled.Add(runId);
                    continue;
                }

                await repository.UpdateAsync(reconciled, cancellationToken);

                _logger.LogInformation(
                    "Deployment {DeploymentId} was left {PreviousStatus} by an earlier API process and is now {Status}.",
                    runId, deployment.Status, reconciled.Status);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                unreconciled.Add(runId);
                _logger.LogWarning(exception, "Failed to reconcile deployment {DeploymentId} left {Status}.",
                    runId, deployment.Status);
            }
        }

        return unreconciled;
    }

    private static async Task<Deployment?> ReconcileAsync(
        IDockerClient docker,
        Deployment deployment,
        ContainerListResponse? container,
        CancellationToken cancellationToken)
    {
        if (deployment.Status == DeploymentStatus.Pending || container is null)
        {
            return deployment.Interrupt();
        }

        if (!FinishedStates.Contains(container.State, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.Equals(container.State, ExitedState, StringComparison.OrdinalIgnoreCase))
        {
            return deployment.Interrupt();
        }

        var inspect = await docker.Containers.InspectContainerAsync(container.ID, cancellationToken);
        return deployment.ProcessDeploymentStatus(inspect.State.ExitCode, null);
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
        catch (DockerContainerNotFoundException)
        {
            _logger.LogDebug("Leftover runner container {ContainerId} was already removed.", containerId);
            return false;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Failed to remove leftover runner container {ContainerId}.", containerId);
            return false;
        }
    }
}
