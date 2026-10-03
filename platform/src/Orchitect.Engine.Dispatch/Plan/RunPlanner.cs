using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Score;

namespace Orchitect.Engine.Dispatch.Plan;

public interface IRunPlanner
{
    /// <summary>
    /// Resolves the score's resource templates and returns what the run should execute. A provision records the
    /// resources, instances and dependency graph and moves the instances to Provisioning; a destroy moves the
    /// recorded instances to Removing. The plan is stored, so a repeat call returns it without recording again.
    /// </summary>
    Task<RunPlan> PlanAsync(DeploymentRunId runId, ScoreFile scoreFile, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the instances a run planned to their final status and, after a successful destroy, releases their
    /// resources. Instances that already left Provisioning or Removing are left alone, so a repeat call is a no-op.
    /// </summary>
    Task FinishAsync(DeploymentRunId runId, RunOutcome outcome, CancellationToken cancellationToken);
}

public sealed partial class RunPlanner : IRunPlanner
{
    private readonly ILogger<RunPlanner> _logger;
    private readonly IDeploymentRunRepository _runRepository;
    private readonly IDeploymentRunPlanRepository _planRepository;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IResourceTemplateRepository _resourceTemplateRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceInstanceRepository _resourceInstanceRepository;
    private readonly IResourceDependencyGraphRepository _resourceDependencyGraphRepository;

    public RunPlanner(ILogger<RunPlanner> logger, IDeploymentRunRepository runRepository,
        IDeploymentRunPlanRepository planRepository, IDeploymentRepository deploymentRepository,
        IApplicationRepository applicationRepository, IResourceTemplateRepository resourceTemplateRepository,
        IResourceRepository resourceRepository, IResourceInstanceRepository resourceInstanceRepository,
        IResourceDependencyGraphRepository resourceDependencyGraphRepository)
    {
        _logger = logger;
        _runRepository = runRepository;
        _planRepository = planRepository;
        _deploymentRepository = deploymentRepository;
        _applicationRepository = applicationRepository;
        _resourceTemplateRepository = resourceTemplateRepository;
        _resourceRepository = resourceRepository;
        _resourceInstanceRepository = resourceInstanceRepository;
        _resourceDependencyGraphRepository = resourceDependencyGraphRepository;
    }

    public async Task<RunPlan> PlanAsync(DeploymentRunId runId, ScoreFile scoreFile,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("orchitect.run.id", runId.Value);

        if (await _planRepository.GetByRunIdAsync(runId, cancellationToken) is { } stored)
        {
            _logger.LogInformation("Run {RunId} is already planned, returning the stored plan.", runId.Value);
            return Deserialize(stored);
        }

        var (run, deployment, application) = await LoadRunAsync(runId, cancellationToken);

        if (run.Status != DeploymentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run '{runId.Value}' cannot be planned while {run.Status}.");
        }

        if (scoreFile.Resources is null || scoreFile.Resources.Count == 0)
        {
            throw new RunPlanException("There are no resources in the score file.");
        }

        var inputs = await ResolveInputsAsync(scoreFile, cancellationToken);
        var context = new RunContext(scoreFile.Metadata.Name, deployment.ApplicationId.Value,
            deployment.EnvironmentId.Value);

        var planned = run.Operation switch
        {
            DeploymentRunOperation.Provision =>
                await PlanProvisionAsync(application, deployment, scoreFile, inputs, context, cancellationToken),
            DeploymentRunOperation.Destroy =>
                await PlanDestroyAsync(application, deployment, scoreFile, inputs, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported run operation '{run.Operation}'.")
        };

        var plan = new RunPlan(context, inputs.Select(i => i.Input).ToList());

        await _planRepository.CreateAsync(
            DeploymentRunPlan.Create(runId, JsonSerializer.Serialize(plan, RunnerContract.JsonOptions), planned),
            cancellationToken);

        _logger.LogInformation("Planned run {RunId} with {InputCount} inputs and {InstanceCount} instances.",
            runId.Value, plan.Inputs.Count, planned.Count);

        return plan;
    }

    public async Task FinishAsync(DeploymentRunId runId, RunOutcome outcome, CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("orchitect.run.id", runId.Value);

        var stored = await _planRepository.GetByRunIdAsync(runId, cancellationToken);

        if (stored is null)
        {
            _logger.LogInformation("Run {RunId} finished before it was planned.", runId.Value);
            return;
        }

        var (run, deployment, application) = await LoadRunAsync(runId, cancellationToken);
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
                    instance.Transition(ResourceInstanceStatus.Active, planned.Output);
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
            await ReleaseResourcesAsync(application, deployment,
                finished.Select(i => i.ResourceId).Distinct().ToList(), cancellationToken);
        }

        _logger.LogInformation("Finished {InstanceCount} instances of run {RunId} as {Outcome}.", finished.Count,
            runId.Value, outcome);
    }

    private async Task<(DeploymentRun Run, Deployment Deployment, Application Application)> LoadRunAsync(
        DeploymentRunId runId, CancellationToken cancellationToken)
    {
        var run = await _runRepository.GetByIdAsync(runId, cancellationToken)
                  ?? throw new InvalidOperationException($"Run '{runId.Value}' was not found.");
        var deployment = await _deploymentRepository.GetByIdAsync(run.DeploymentId, cancellationToken)
                         ?? throw new InvalidOperationException(
                             $"Deployment '{run.DeploymentId.Value}' of run '{runId.Value}' was not found.");
        var application = await _applicationRepository.GetByIdAsync(deployment.ApplicationId, cancellationToken)
                          ?? throw new InvalidOperationException(
                              $"Application '{deployment.ApplicationId.Value}' of run '{runId.Value}' was not found.");

        return (run, deployment, application);
    }

    private async Task<List<ResolvedInput>> ResolveInputsAsync(ScoreFile scoreFile,
        CancellationToken cancellationToken)
    {
        var inputs = new List<ResolvedInput>();

        foreach (var (key, resource) in scoreFile.Resources!)
        {
            var type = resource.Type.Trim().ToLower();
            var parameters = resource.Parameters ?? [];

            var template = await _resourceTemplateRepository.GetByTypeAsync(type, cancellationToken)
                           ?? throw new RunPlanException(
                               $"No resource template found for type '{type}' (resource '{key}').");
            var version = template.GetLatestVersion()
                          ?? throw new RunPlanException(
                              $"Resource template '{template.Type}' has no active version (resource '{key}').");

            var input = new RunInput(
                Key: key,
                TemplateName: template.Name,
                TemplateType: template.Type,
                Provider: ToRunInputProvider(template.Provider),
                Source: new RunInputSource(version.Source.BaseUrl, version.Source.Tag,
                    string.IsNullOrEmpty(version.Source.FolderPath) ? null : version.Source.FolderPath),
                Parameters: parameters);

            inputs.Add(new ResolvedInput(input, template, version, resource));
        }

        return inputs;
    }

    private async Task<List<PlannedResourceInstance>> PlanProvisionAsync(Application application,
        Deployment deployment, ScoreFile scoreFile, List<ResolvedInput> inputs, RunContext context,
        CancellationToken cancellationToken)
    {
        var instances = await RecordResourcesAsync(application, deployment, scoreFile, inputs, cancellationToken);
        await TransitionAsync(instances, BeginProvisioning, cancellationToken);
        return instances.Select(i => new PlannedResourceInstance(i.Id, CreateOutput(i, inputs, context))).ToList();
    }

    private async Task<List<PlannedResourceInstance>> PlanDestroyAsync(Application application,
        Deployment deployment, ScoreFile scoreFile, List<ResolvedInput> inputs, CancellationToken cancellationToken)
    {
        var resources = await FindRecordedResourcesAsync(application, deployment, scoreFile, inputs,
            cancellationToken);
        var instances = await FindRemovableInstancesAsync(resources, cancellationToken);
        await TransitionAsync(instances, BeginRemoval, cancellationToken);
        return instances.Select(i => new PlannedResourceInstance(i.Id, null)).ToList();
    }

    private async Task<List<ResourceInstance>> RecordResourcesAsync(Application application, Deployment deployment,
        ScoreFile scoreFile, List<ResolvedInput> inputs, CancellationToken cancellationToken)
    {
        var knownResources =
            (await _resourceRepository.GetByEnvironmentAsync(deployment.EnvironmentId, cancellationToken)).ToList();
        var existingGraph =
            await _resourceDependencyGraphRepository.GetByEnvironmentAsync(deployment.EnvironmentId,
                cancellationToken);
        var graph = existingGraph ??
                    ResourceDependencyGraph.Create(application.OrganisationId, deployment.EnvironmentId);

        var resourcesByKey = new Dictionary<string, Resource>();
        var instancesByResource = new Dictionary<ResourceId, ResourceInstance>();

        foreach (var input in inputs)
        {
            var resource = await GetOrCreateResourceAsync(application, deployment, input, knownResources,
                cancellationToken);
            graph.AddResource(resource.Id);
            resourcesByKey[input.Input.Key] = resource;

            if (!instancesByResource.ContainsKey(resource.Id))
            {
                instancesByResource[resource.Id] = await GetOrCreateInstanceAsync(resource, input, cancellationToken);
            }
        }

        foreach (var resource in resourcesByKey.Values.DistinctBy(r => r.Id))
        {
            if (!resource.Consumers.Contains(application.Id))
            {
                resource.AddConsumer(application.Id);
                await _resourceRepository.UpdateAsync(resource, cancellationToken);
            }
        }

        foreach (var keys in resourcesByKey.GroupBy(r => r.Value.Id, r => r.Key))
        {
            var dependencies = keys
                .SelectMany(key => FindReferencedResourceKeys(scoreFile.Resources![key]))
                .Where(resourcesByKey.ContainsKey)
                .Select(key => resourcesByKey[key].Id)
                .Where(id => id != keys.Key);

            graph.SetDependencies(keys.Key, dependencies);
        }

        if (existingGraph is null)
        {
            await _resourceDependencyGraphRepository.CreateAsync(graph, cancellationToken);
        }
        else
        {
            await _resourceDependencyGraphRepository.UpdateAsync(graph, cancellationToken);
        }

        return instancesByResource.Values.ToList();
    }

    private async Task<Resource> GetOrCreateResourceAsync(Application application, Deployment deployment,
        ResolvedInput input, List<Resource> knownResources, CancellationToken cancellationToken)
    {
        var name = ResolveResourceName(application, input.Input.Key, input.ScoreResource);
        var slug = Resource.CreateSlug(name);
        var existing = knownResources.FirstOrDefault(r => r.Slug == slug);

        if (existing is not null)
        {
            if (existing.ResourceTemplateId != input.Template.Id)
            {
                throw new RunPlanException(
                    $"Resource '{slug}' already exists with a different resource template (resource '{input.Input.Key}').");
            }

            return existing;
        }

        var resource = Resource.Create(new CreateResourceRequest(
            OrganisationId: application.OrganisationId,
            Name: name,
            Description: input.ScoreResource.Metadata?.Annotations?.GetValueOrDefault("description") ?? string.Empty,
            ResourceTemplateId: input.Template.Id,
            EnvironmentId: deployment.EnvironmentId,
            Kind: Enum.TryParse<ResourceKind>(input.ScoreResource.Class, ignoreCase: true, out var kind)
                ? kind
                : ResourceKind.Direct,
            ApplicationId: application.Id));

        var created = await _resourceRepository.CreateAsync(resource, cancellationToken)
                      ?? throw new InvalidOperationException($"Unable to create resource '{slug}'.");
        knownResources.Add(created);
        return created;
    }

    private async Task<ResourceInstance> GetOrCreateInstanceAsync(Resource resource, ResolvedInput input,
        CancellationToken cancellationToken)
    {
        var inputParameters = input.Input.Parameters
            .ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));

        var existingInstances = await _resourceInstanceRepository.GetByResourceAsync(resource.Id, cancellationToken);
        var current = existingInstances
            .Where(i => i.Status != ResourceInstanceStatus.Removed)
            .MaxBy(i => i.CreatedAt);

        if (current is not null)
        {
            current.Reconfigure(input.Version.Id, inputParameters);
            return current;
        }

        var instance = ResourceInstance.Create(new CreateResourceInstanceRequest(
            ResourceId: resource.Id,
            OrganisationId: resource.OrganisationId,
            Name: $"{resource.Slug}-instance",
            TemplateVersionId: input.Version.Id,
            EnvironmentId: resource.EnvironmentId,
            InputParameters: inputParameters));

        return await _resourceInstanceRepository.CreateAsync(instance, cancellationToken)
               ?? throw new InvalidOperationException($"Unable to create an instance of resource '{resource.Slug}'.");
    }

    private async Task<List<Resource>> FindRecordedResourcesAsync(Application application, Deployment deployment,
        ScoreFile scoreFile, List<ResolvedInput> inputs, CancellationToken cancellationToken)
    {
        var existingResources =
            await _resourceRepository.GetByEnvironmentAsync(deployment.EnvironmentId, cancellationToken);
        var slugs = inputs
            .Select(i => Resource.CreateSlug(ResolveResourceName(application, i.Input.Key,
                scoreFile.Resources![i.Input.Key])))
            .ToHashSet();

        return existingResources.Where(r => slugs.Contains(r.Slug)).ToList();
    }

    private async Task<List<ResourceInstance>> FindRemovableInstancesAsync(List<Resource> resources,
        CancellationToken cancellationToken)
    {
        var instances = new List<ResourceInstance>();

        foreach (var resource in resources)
        {
            var existingInstances =
                await _resourceInstanceRepository.GetByResourceAsync(resource.Id, cancellationToken);

            instances.AddRange(existingInstances.Where(i => i.Status != ResourceInstanceStatus.Removed));
        }

        return instances;
    }

    private async Task ReleaseResourcesAsync(Application application, Deployment deployment,
        List<ResourceId> resourceIds, CancellationToken cancellationToken)
    {
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

    private async Task TransitionAsync(List<ResourceInstance> instances, Action<ResourceInstance> transition,
        CancellationToken cancellationToken)
    {
        foreach (var instance in instances)
        {
            transition(instance);
            await _resourceInstanceRepository.UpdateAsync(instance, cancellationToken);
        }
    }

    private static void BeginProvisioning(ResourceInstance instance)
    {
        if (instance.Status == ResourceInstanceStatus.Provisioning)
        {
            instance.Transition(ResourceInstanceStatus.Failed);
        }

        if (instance.Status == ResourceInstanceStatus.Failed)
        {
            instance.Transition(ResourceInstanceStatus.Pending);
        }

        instance.Transition(ResourceInstanceStatus.Provisioning);
    }

    private static void BeginRemoval(ResourceInstance instance)
    {
        if (instance.Status == ResourceInstanceStatus.Provisioning)
        {
            instance.Transition(ResourceInstanceStatus.Failed);
        }

        if (instance.Status == ResourceInstanceStatus.Removing)
        {
            instance.Transition(ResourceInstanceStatus.RemovalFailed);
        }

        if (instance.Status != ResourceInstanceStatus.PendingRemoval)
        {
            instance.Transition(ResourceInstanceStatus.PendingRemoval);
        }

        instance.Transition(ResourceInstanceStatus.Removing);
    }

    private static ResourceInstanceOutput CreateOutput(ResourceInstance instance, List<ResolvedInput> inputs,
        RunContext context)
    {
        var version = inputs
            .Select(i => i.Version)
            .First(v => v.Id == instance.TemplateVersionId);

        return new ResourceInstanceOutput { Location = version.Source.BaseUrl, Workspace = context.ProjectName };
    }

    private static RunPlan Deserialize(DeploymentRunPlan stored) =>
        JsonSerializer.Deserialize<RunPlan>(stored.Contents, RunnerContract.JsonOptions)
        ?? throw new InvalidOperationException($"The stored plan of run '{stored.RunId.Value}' is empty.");

    private static RunInputProvider ToRunInputProvider(ResourceTemplateProvider provider) => provider switch
    {
        ResourceTemplateProvider.Terraform => RunInputProvider.Terraform,
        ResourceTemplateProvider.Helm => RunInputProvider.Helm,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };

    private static string ResolveResourceName(Application application, string key, ScoreResource scoreResource) =>
        scoreResource.Id ?? $"{application.Name}-{key}";

    private static IEnumerable<string> FindReferencedResourceKeys(ScoreResource scoreResource) =>
        (scoreResource.Parameters ?? [])
        .SelectMany(p => ResourceReferenceRegex().Matches(p.Value))
        .Select(m => m.Groups["key"].Value)
        .Distinct();

    [GeneratedRegex(@"\$\{resources\.(?<key>[A-Za-z0-9_-]+)\.")]
    private static partial Regex ResourceReferenceRegex();

    private sealed record ResolvedInput(
        RunInput Input,
        ResourceTemplate Template,
        ResourceTemplateVersion Version,
        ScoreResource ScoreResource);
}
