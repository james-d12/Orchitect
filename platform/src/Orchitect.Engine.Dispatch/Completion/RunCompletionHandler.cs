using Microsoft.Extensions.Logging;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Engine.Dispatch.Completion;

public interface IRunCompletionHandler
{
    /// <summary>
    /// Completes a run: records its result, settles its resource instances, finishes its deployment and revokes its
    /// token. Returns false when the run had already completed, in which case only a missing exit code is recorded.
    /// </summary>
    Task<bool> CompleteAsync(DeploymentRunId runId, RunResult result, CancellationToken cancellationToken);
}

public sealed class RunCompletionHandler : IRunCompletionHandler
{
    private readonly IDeploymentRunRepository _runs;
    private readonly IDeploymentRepository _deployments;
    private readonly IResourceInstanceRepository _instances;
    private readonly IResourceRepository _resources;
    private readonly IResourceTemplateRepository _templates;
    private readonly IResourceDependencyGraphRepository _graphs;
    private readonly ILogger<RunCompletionHandler> _logger;

    public RunCompletionHandler(
        IDeploymentRunRepository runs,
        IDeploymentRepository deployments,
        IResourceInstanceRepository instances,
        IResourceRepository resources,
        IResourceTemplateRepository templates,
        IResourceDependencyGraphRepository graphs,
        ILogger<RunCompletionHandler> logger)
    {
        _runs = runs;
        _deployments = deployments;
        _instances = instances;
        _resources = resources;
        _templates = templates;
        _graphs = graphs;
        _logger = logger;
    }

    public async Task<bool> CompleteAsync(DeploymentRunId runId, RunResult result,
        CancellationToken cancellationToken)
    {
        var run = await _runs.GetByIdAsync(runId, cancellationToken)
                  ?? throw new InvalidOperationException($"Run '{runId.Value}' was not found.");
        var deployment = await _deployments.GetByIdAsync(run.DeploymentId, cancellationToken);

        if (run.IsActive)
        {
            var finished = result.Outcome == RunOutcome.Succeeded
                ? run.Succeed(result.ExitCode, result.RunnerId)
                : run.Fail(string.IsNullOrWhiteSpace(result.ErrorSummary)
                    ? RunResult.ReportedFailureSummary
                    : result.ErrorSummary, result.ExitCode, result.RunnerId);

            if (await _runs.TryFinishAsync(finished, cancellationToken))
            {
                await FinishDeploymentAsync(finished, deployment, cancellationToken);
                await SettleInstancesAsync(finished, deployment, cancellationToken);

                _logger.LogInformation("Run {RunId} of deployment {DeploymentId} is {Status}.",
                    finished.Id.Value, finished.DeploymentId.Value, finished.Status);
                return true;
            }

            run = await _runs.GetByIdAsync(runId, cancellationToken)
                  ?? throw new InvalidOperationException($"Run '{runId.Value}' was not found.");
        }

        if (result.ExitCode is { } exitCode)
        {
            var recorded = run.RecordExit(exitCode, result.RunnerId);

            if (recorded != run)
            {
                run = await _runs.UpdateAsync(recorded, cancellationToken) ?? recorded;
            }
        }

        await FinishDeploymentAsync(run, deployment, cancellationToken);
        return false;
    }

    private async Task FinishDeploymentAsync(DeploymentRun run, Deployment? deployment,
        CancellationToken cancellationToken)
    {
        var owned = run.Operation == DeploymentRunOperation.Destroy
            ? deployment?.Status == DeploymentStatus.Destroying
            : deployment?.Status is DeploymentStatus.Pending or DeploymentStatus.Deploying;

        if (deployment is null || !owned)
        {
            return;
        }

        var latest = await _runs.GetLatestAsync(deployment.Id, cancellationToken);

        if (latest?.Id != run.Id)
        {
            return;
        }

        var finished = run.Status == DeploymentRunStatus.Succeeded
            ? deployment.Succeed()
            : deployment.Fail(run.ErrorSummary ?? RunResult.ReportedFailureSummary);

        await _deployments.UpdateAsync(finished, cancellationToken);
    }

    private async Task SettleInstancesAsync(DeploymentRun run, Deployment? deployment,
        CancellationToken cancellationToken)
    {
        var succeeded = run.Status == DeploymentRunStatus.Succeeded;
        var instances = new List<ResourceInstance>();

        foreach (var instanceId in run.InstanceIds)
        {
            var instance = await _instances.GetByIdAsync(instanceId, cancellationToken);

            if (instance is null)
            {
                continue;
            }

            instances.Add(instance);

            switch (instance.Status)
            {
                case ResourceInstanceStatus.Provisioning when succeeded:
                    instance.Transition(ResourceInstanceStatus.Active,
                        await CreateOutputAsync(instance, run, cancellationToken));
                    break;
                case ResourceInstanceStatus.Provisioning:
                    instance.Transition(ResourceInstanceStatus.Failed);
                    break;
                case ResourceInstanceStatus.Removing:
                    instance.Transition(succeeded
                        ? ResourceInstanceStatus.Removed
                        : ResourceInstanceStatus.RemovalFailed);
                    break;
                default:
                    continue;
            }

            await _instances.UpdateAsync(instance, cancellationToken);
        }

        if (run.Operation == DeploymentRunOperation.Destroy && succeeded && deployment is not null)
        {
            await ReleaseResourcesAsync(deployment, instances.Select(i => i.ResourceId).Distinct().ToList(),
                cancellationToken);
        }
    }

    private async Task<ResourceInstanceOutput> CreateOutputAsync(ResourceInstance instance, DeploymentRun run,
        CancellationToken cancellationToken)
    {
        var resource = await _resources.GetByIdAsync(instance.ResourceId, cancellationToken)
                       ?? throw new InvalidOperationException(
                           $"Resource '{instance.ResourceId.Value}' of instance '{instance.Id.Value}' was not found.");
        var template = await _templates.GetByIdAsync(resource.ResourceTemplateId, cancellationToken);
        var version = template?.Versions.FirstOrDefault(v => v.Id == instance.TemplateVersionId)
                      ?? throw new InvalidOperationException(
                          $"Template version '{instance.TemplateVersionId.Value}' of instance '{instance.Id.Value}' was not found.");

        return new ResourceInstanceOutput { Location = version.Source.BaseUrl, Workspace = run.ProjectName };
    }

    private async Task ReleaseResourcesAsync(Deployment deployment, List<ResourceId> resourceIds,
        CancellationToken cancellationToken)
    {
        foreach (var resourceId in resourceIds)
        {
            var resource = await _resources.GetByIdAsync(resourceId, cancellationToken);

            if (resource is null)
            {
                continue;
            }

            resource.RemoveConsumer(deployment.ApplicationId);
            await _resources.UpdateAsync(resource, cancellationToken);
        }

        var graph = await _graphs.GetByEnvironmentAsync(deployment.EnvironmentId, cancellationToken);

        if (graph is null)
        {
            return;
        }

        foreach (var resourceId in resourceIds)
        {
            graph.RemoveResource(resourceId);
        }

        await _graphs.UpdateAsync(graph, cancellationToken);
    }
}
