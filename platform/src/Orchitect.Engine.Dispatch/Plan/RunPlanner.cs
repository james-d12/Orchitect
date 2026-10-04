using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core;
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
    /// resources, instances and dependency graph and moves the instances to Provisioning; a destroy plans each
    /// recorded resource from the template version its instance was provisioned with and moves the recorded
    /// instances to Removing. The records and the plan are stored in one transaction, so a repeat or
    /// concurrent call returns the stored plan without recording again. Throws <see cref="RunPlanException"/> when
    /// the run or the score can't be planned, including when the run was asked to cancel before it was planned.
    /// </summary>
    Task<RunPlan> PlanAsync(DeploymentRunId runId, ScoreFile scoreFile, CancellationToken cancellationToken);
}

public sealed partial class RunPlanner : IRunPlanner
{
    private readonly ILogger<RunPlanner> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeploymentRunRepository _runRepository;
    private readonly IDeploymentRunPlanRepository _planRepository;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IResourceTemplateRepository _resourceTemplateRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceInstanceRepository _resourceInstanceRepository;
    private readonly IResourceDependencyGraphRepository _resourceDependencyGraphRepository;

    public RunPlanner(ILogger<RunPlanner> logger, IUnitOfWork unitOfWork, RunRepositories runRepositories,
        IResourceTemplateRepository resourceTemplateRepository, ResourceRepositories resourceRepositories)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _runRepository = runRepositories.Runs;
        _planRepository = runRepositories.Plans;
        _deploymentRepository = runRepositories.Deployments;
        _applicationRepository = runRepositories.Applications;
        _resourceTemplateRepository = resourceTemplateRepository;
        _resourceRepository = resourceRepositories.Resources;
        _resourceInstanceRepository = resourceRepositories.Instances;
        _resourceDependencyGraphRepository = resourceRepositories.DependencyGraphs;
    }

    public async Task<RunPlan> PlanAsync(DeploymentRunId runId, ScoreFile scoreFile,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();
        activity?.SetTag("orchitect.run.id", runId.Value);

        if (scoreFile?.Metadata is null)
        {
            throw Invalid("The submission has no score file.");
        }

        if (scoreFile.Resources is null || scoreFile.Resources.Count == 0)
        {
            throw Invalid("There are no resources in the score file.");
        }

        return await _unitOfWork.ExecuteAsync(token => PlanInTransactionAsync(runId, scoreFile, token),
            cancellationToken);
    }

    private async Task<RunPlan> PlanInTransactionAsync(DeploymentRunId runId, ScoreFile scoreFile,
        CancellationToken cancellationToken)
    {
        await _runRepository.LockAsync(runId, cancellationToken);

        if (await _planRepository.GetByRunIdAsync(runId, cancellationToken) is { } stored)
        {
            return ReturnStoredPlan(stored, scoreFile);
        }

        var run = await _runRepository.GetByIdAsync(runId, cancellationToken)
                  ?? throw new RunPlanException(RunPlanFailure.RunNotFound, $"Run '{runId.Value}' was not found.");

        if (run.Status != DeploymentRunStatus.Running)
        {
            throw new RunPlanException(RunPlanFailure.RunNotRunning,
                $"Run '{runId.Value}' cannot be planned while {run.Status}.");
        }

        if (run.CancelRequestedAt is not null)
        {
            throw new RunPlanException(RunPlanFailure.RunNotRunning,
                $"Run '{runId.Value}' cannot be planned because it was asked to cancel.");
        }

        var deployment = await _deploymentRepository.GetByIdAsync(run.DeploymentId, cancellationToken)
                         ?? throw new InvalidOperationException(
                             $"Deployment '{run.DeploymentId.Value}' of run '{runId.Value}' was not found.");
        var application = await _applicationRepository.GetByIdAsync(deployment.ApplicationId, cancellationToken)
                          ?? throw new InvalidOperationException(
                              $"Application '{deployment.ApplicationId.Value}' of run '{runId.Value}' was not found.");

        var knownResources =
            (await _resourceRepository.GetByEnvironmentAsync(deployment.EnvironmentId, cancellationToken)).ToList();
        var recordedVersions = run.Operation == DeploymentRunOperation.Destroy
            ? await GetRecordedVersionsAsync(application, scoreFile, knownResources, cancellationToken)
            : [];
        var inputs = await ResolveInputsAsync(application, scoreFile, recordedVersions, cancellationToken);

        var planned = run.Operation switch
        {
            DeploymentRunOperation.Provision =>
                await PlanProvisionAsync(application, deployment, scoreFile, inputs, knownResources,
                    cancellationToken),
            DeploymentRunOperation.Destroy => await PlanDestroyAsync(inputs, knownResources, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported run operation '{run.Operation}'.")
        };

        var plan = new RunPlan(
            new RunContext(scoreFile.Metadata.Name, deployment.ApplicationId.Value, deployment.EnvironmentId.Value),
            inputs.Select(i => i.Input).ToList());

        await _planRepository.CreateAsync(DeploymentRunPlan.Create(runId, StoredRunPlan.Serialize(plan), planned),
            cancellationToken);

        _logger.LogInformation("Planned run {RunId} with {InputCount} inputs and {InstanceCount} instances.",
            runId.Value, plan.Inputs.Count, planned.Count);

        return plan;
    }

    private RunPlan ReturnStoredPlan(DeploymentRunPlan stored, ScoreFile scoreFile)
    {
        var plan = StoredRunPlan.Deserialize(stored);

        if (Matches(plan, scoreFile))
        {
            _logger.LogInformation("Run {RunId} is already planned, returning the stored plan.", stored.RunId.Value);
        }
        else
        {
            _logger.LogWarning(
                "Run {RunId} is already planned from a different score file, returning the stored plan.",
                stored.RunId.Value);
        }

        return plan;
    }

    private async Task<Dictionary<string, ResourceTemplateVersionId>> GetRecordedVersionsAsync(
        Application application, ScoreFile scoreFile, List<Resource> knownResources,
        CancellationToken cancellationToken)
    {
        var slugs = scoreFile.Resources!
            .Select(r => Resource.CreateSlug(ResourceName(application, r.Key, r.Value)))
            .ToHashSet();
        var recordedVersions = new Dictionary<string, ResourceTemplateVersionId>();

        foreach (var resource in knownResources.Where(r => slugs.Contains(r.Slug)))
        {
            var instances = await _resourceInstanceRepository.GetByResourceAsync(resource.Id, cancellationToken);
            var current = instances
                .Where(i => i.Status != ResourceInstanceStatus.Removed)
                .MaxBy(i => i.CreatedAt);

            if (current is not null)
            {
                recordedVersions[resource.Slug] = current.TemplateVersionId;
            }
        }

        return recordedVersions;
    }

    private async Task<List<ResolvedInput>> ResolveInputsAsync(Application application, ScoreFile scoreFile,
        Dictionary<string, ResourceTemplateVersionId> recordedVersions, CancellationToken cancellationToken)
    {
        var inputs = new List<ResolvedInput>();

        foreach (var (key, resource) in scoreFile.Resources!)
        {
            var type = resource.Type.Trim().ToLower();
            var parameters = resource.Parameters ?? [];

            var name = ResourceName(application, key, resource);
            var slug = Resource.CreateSlug(name);

            var template = await _resourceTemplateRepository.GetByTypeAsync(type, cancellationToken)
                           ?? throw Invalid($"No resource template found for type '{type}' (resource '{key}').");
            var version = recordedVersions.TryGetValue(slug, out var recordedVersionId)
                ? template.Versions.FirstOrDefault(v => v.Id == recordedVersionId)
                  ?? throw Invalid(
                      $"Resource '{slug}' was provisioned with a different resource template (resource '{key}').")
                : template.GetLatestVersion()
                  ?? throw Invalid($"Resource template '{template.Type}' has no active version (resource '{key}').");

            var input = new RunInput(
                Key: key,
                TemplateName: template.Name,
                TemplateType: template.Type,
                Provider: ToRunInputProvider(template.Provider),
                Source: new RunInputSource(version.Source.BaseUrl, version.Source.Tag,
                    string.IsNullOrEmpty(version.Source.FolderPath) ? null : version.Source.FolderPath),
                Parameters: parameters);

            inputs.Add(new ResolvedInput(input, template, version, resource, name, slug));
        }

        return inputs;
    }

    private async Task<List<PlannedResourceInstance>> PlanProvisionAsync(Application application,
        Deployment deployment, ScoreFile scoreFile, List<ResolvedInput> inputs, List<Resource> knownResources,
        CancellationToken cancellationToken)
    {
        EnsureOneTemplatePerResource(inputs, knownResources);

        var instances = await RecordResourcesAsync(application, deployment, scoreFile, inputs, knownResources,
            cancellationToken);

        foreach (var (instance, _) in instances)
        {
            BeginProvisioning(instance);
            await _resourceInstanceRepository.UpdateAsync(instance, cancellationToken);
        }

        return instances.Select(i => new PlannedResourceInstance(i.Instance.Id, i.Key)).ToList();
    }

    private async Task<List<PlannedResourceInstance>> PlanDestroyAsync(List<ResolvedInput> inputs,
        List<Resource> knownResources, CancellationToken cancellationToken)
    {
        var keysBySlug = inputs.GroupBy(i => i.Slug).ToDictionary(g => g.Key, g => g.First().Input.Key);
        var planned = new List<PlannedResourceInstance>();

        foreach (var resource in knownResources.Where(r => keysBySlug.ContainsKey(r.Slug)))
        {
            var instances = await _resourceInstanceRepository.GetByResourceAsync(resource.Id, cancellationToken);

            foreach (var instance in instances.Where(i => i.Status != ResourceInstanceStatus.Removed))
            {
                BeginRemoval(instance);
                await _resourceInstanceRepository.UpdateAsync(instance, cancellationToken);
                planned.Add(new PlannedResourceInstance(instance.Id, keysBySlug[resource.Slug]));
            }
        }

        return planned;
    }

    private static void EnsureOneTemplatePerResource(List<ResolvedInput> inputs, List<Resource> knownResources)
    {
        foreach (var input in inputs)
        {
            var templateId = knownResources.FirstOrDefault(r => r.Slug == input.Slug)?.ResourceTemplateId
                             ?? inputs.First(i => i.Slug == input.Slug).Template.Id;

            if (templateId != input.Template.Id)
            {
                throw Invalid(
                    $"Resource '{input.Slug}' already exists with a different resource template (resource '{input.Input.Key}').");
            }
        }
    }

    private async Task<List<(ResourceInstance Instance, string Key)>> RecordResourcesAsync(
        Application application, Deployment deployment, ScoreFile scoreFile, List<ResolvedInput> inputs,
        List<Resource> knownResources, CancellationToken cancellationToken)
    {
        var existingGraph =
            await _resourceDependencyGraphRepository.GetByEnvironmentAsync(deployment.EnvironmentId,
                cancellationToken);
        var graph = existingGraph ??
                    ResourceDependencyGraph.Create(application.OrganisationId, deployment.EnvironmentId);

        var resourcesByKey = new Dictionary<string, Resource>();
        var instancesByResource = new Dictionary<ResourceId, (ResourceInstance Instance, string Key)>();

        foreach (var input in inputs)
        {
            var resource = await GetOrCreateResourceAsync(application, deployment, input, knownResources,
                cancellationToken);
            graph.AddResource(resource.Id);
            resourcesByKey[input.Input.Key] = resource;

            if (!instancesByResource.ContainsKey(resource.Id))
            {
                instancesByResource[resource.Id] =
                    (await GetOrCreateInstanceAsync(resource, input, cancellationToken), input.Input.Key);
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
        if (knownResources.FirstOrDefault(r => r.Slug == input.Slug) is { } existing)
        {
            return existing;
        }

        var resource = Resource.Create(new CreateResourceRequest(
            OrganisationId: application.OrganisationId,
            Name: input.Name,
            Description: input.ScoreResource.Metadata?.Annotations?.GetValueOrDefault("description") ?? string.Empty,
            ResourceTemplateId: input.Template.Id,
            EnvironmentId: deployment.EnvironmentId,
            Kind: Enum.TryParse<ResourceKind>(input.ScoreResource.Class, ignoreCase: true, out var kind)
                ? kind
                : ResourceKind.Direct,
            ApplicationId: application.Id));

        var created = await _resourceRepository.CreateAsync(resource, cancellationToken)
                      ?? throw new InvalidOperationException($"Unable to create resource '{input.Slug}'.");
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

    private static string ResourceName(Application application, string key, ScoreResource resource) =>
        resource.Id ?? $"{application.Name}-{key}";

    private static bool Matches(RunPlan plan, ScoreFile scoreFile) =>
        plan.Inputs.Count == scoreFile.Resources!.Count &&
        plan.Inputs.All(input =>
            scoreFile.Resources.TryGetValue(input.Key, out var resource) &&
            string.Equals(resource.Type.Trim(), input.TemplateType, StringComparison.OrdinalIgnoreCase) &&
            (resource.Parameters ?? []).Count == input.Parameters.Count &&
            (resource.Parameters ?? []).All(p =>
                input.Parameters.TryGetValue(p.Key, out var value) && value == p.Value));

    private static RunPlanException Invalid(string message) => new(RunPlanFailure.Invalid, message);

    private static RunInputProvider ToRunInputProvider(ResourceTemplateProvider provider) => provider switch
    {
        ResourceTemplateProvider.Terraform => RunInputProvider.Terraform,
        ResourceTemplateProvider.Helm => RunInputProvider.Helm,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };

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
        ScoreResource ScoreResource,
        string Name,
        string Slug);
}
