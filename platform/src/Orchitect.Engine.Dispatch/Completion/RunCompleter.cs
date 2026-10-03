using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Plan;

namespace Orchitect.Engine.Dispatch.Completion;

public interface IRunCompleter
{
    /// <summary>
    /// Moves the instances a run planned to their final status and, after a successful destroy, releases their
    /// resources. Instances that already left Provisioning or Removing are left alone, so a repeat call is a no-op.
    /// </summary>
    Task CompleteAsync(DeploymentRunId runId, RunOutcome outcome, CancellationToken cancellationToken);
}

public sealed class RunCompleter : IRunCompleter
{
    private readonly ILogger<RunCompleter> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeploymentRunRepository _runRepository;
    private readonly IDeploymentRunPlanRepository _planRepository;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceInstanceRepository _resourceInstanceRepository;
    private readonly IResourceDependencyGraphRepository _resourceDependencyGraphRepository;

    public RunCompleter(ILogger<RunCompleter> logger, IUnitOfWork unitOfWork,
        IDeploymentRunRepository runRepository, IDeploymentRunPlanRepository planRepository,
        IDeploymentRepository deploymentRepository, IApplicationRepository applicationRepository,
        IResourceRepository resourceRepository, IResourceInstanceRepository resourceInstanceRepository,
        IResourceDependencyGraphRepository resourceDependencyGraphRepository)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _runRepository = runRepository;
        _planRepository = planRepository;
        _deploymentRepository = deploymentRepository;
        _applicationRepository = applicationRepository;
        _resourceRepository = resourceRepository;
        _resourceInstanceRepository = resourceInstanceRepository;
        _resourceDependencyGraphRepository = resourceDependencyGraphRepository;
    }

    public async Task CompleteAsync(DeploymentRunId runId, RunOutcome outcome, CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("orchitect.run.id", runId.Value);

        await _unitOfWork.ExecuteAsync(token => CompleteInTransactionAsync(runId, outcome, token),
            cancellationToken);
    }

    private async Task CompleteInTransactionAsync(DeploymentRunId runId, RunOutcome outcome,
        CancellationToken cancellationToken)
    {
        await _runRepository.LockAsync(runId, cancellationToken);

        var stored = await _planRepository.GetByRunIdAsync(runId, cancellationToken);

        if (stored is null)
        {
            _logger.LogInformation("Run {RunId} finished before it was planned.", runId.Value);
            return;
        }

        var run = await _runRepository.GetByIdAsync(runId, cancellationToken)
                  ?? throw new InvalidOperationException($"Run '{runId.Value}' was not found.");
        var plan = StoredRunPlan.Deserialize(stored);
        var inputs = plan.Inputs.ToDictionary(i => i.Key);
        var succeeded = outcome == RunOutcome.Succeeded;
        var finished = new List<ResourceInstance>();

        foreach (var planned in stored.Instances)
        {
            var instance = await _resourceInstanceRepository.GetByIdAsync(planned.InstanceId, cancellationToken);

            if (instance is null)
            {
                continue;
            }

            switch (instance.Status)
            {
                case ResourceInstanceStatus.Provisioning when succeeded:
                    instance.Transition(ResourceInstanceStatus.Active,
                        CreateOutput(inputs[planned.Key], plan.Context));
                    break;
                case ResourceInstanceStatus.Provisioning:
                    instance.Transition(ResourceInstanceStatus.Failed);
                    break;
                case ResourceInstanceStatus.Removing when succeeded:
                    instance.Transition(ResourceInstanceStatus.Removed);
                    break;
                case ResourceInstanceStatus.Removing:
                    instance.Transition(ResourceInstanceStatus.RemovalFailed);
                    break;
                default:
                    continue;
            }

            await _resourceInstanceRepository.UpdateAsync(instance, cancellationToken);
            finished.Add(instance);
        }

        if (run.Operation == DeploymentRunOperation.Destroy && succeeded && finished.Count > 0)
        {
            await ReleaseResourcesAsync(run, finished.Select(i => i.ResourceId).Distinct().ToList(),
                cancellationToken);
        }

        _logger.LogInformation("Finished {InstanceCount} instances of run {RunId} as {Outcome}.", finished.Count,
            runId.Value, outcome);
    }

    private async Task ReleaseResourcesAsync(DeploymentRun run, List<ResourceId> resourceIds,
        CancellationToken cancellationToken)
    {
        var deployment = await _deploymentRepository.GetByIdAsync(run.DeploymentId, cancellationToken)
                         ?? throw new InvalidOperationException(
                             $"Deployment '{run.DeploymentId.Value}' of run '{run.Id.Value}' was not found.");
        var application = await _applicationRepository.GetByIdAsync(deployment.ApplicationId, cancellationToken)
                          ?? throw new InvalidOperationException(
                              $"Application '{deployment.ApplicationId.Value}' of run '{run.Id.Value}' was not found.");

        foreach (var resourceId in resourceIds)
        {
            var resource = await _resourceRepository.GetByIdAsync(resourceId, cancellationToken);

            if (resource is null)
            {
                continue;
            }

            resource.RemoveConsumer(application.Id);
            await _resourceRepository.UpdateAsync(resource, cancellationToken);
        }

        var graph = await _resourceDependencyGraphRepository.GetByEnvironmentAsync(deployment.EnvironmentId,
            cancellationToken);

        if (graph is null)
        {
            return;
        }

        foreach (var resourceId in resourceIds)
        {
            graph.RemoveResource(resourceId);
        }

        await _resourceDependencyGraphRepository.UpdateAsync(graph, cancellationToken);
    }

    private static ResourceInstanceOutput CreateOutput(RunInput input, RunContext context) =>
        new() { Location = input.Source.BaseUrl, Workspace = context.ProjectName };
}
