using System.Text.Json;
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
    /// instances to Removing. A score resource whose previous keys match a recorded resource keeps that resource,
    /// which a provision renames, instead of recording a new one. The records and the plan are stored in one
    /// transaction, so a repeat or concurrent call returns the stored plan without recording again. Throws
    /// <see cref="RunPlanException"/> when the run or the score can't be planned, including when the run was asked
    /// to cancel before it was planned.
    /// </summary>
    Task<RunPlan> PlanAsync(DeploymentRunId runId, ScoreFile scoreFile, CancellationToken cancellationToken);
}

public sealed class RunPlanner : IRunPlanner
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
            await _resourceRepository.GetByEnvironmentAsync(deployment.EnvironmentId, cancellationToken);
        var matchedResources = MatchKnownResources(application, scoreFile, knownResources);
        var removableInstances = run.Operation == DeploymentRunOperation.Destroy
            ? await GetRemovableInstancesAsync(matchedResources, cancellationToken)
            : [];
        var recordedVersions = removableInstances.ToDictionary(r => r.Key,
            r => r.Value.MaxBy(i => i.CreatedAt)!.TemplateVersionId);
        var inputs = await ResolveInputsAsync(application, scoreFile, recordedVersions, cancellationToken);

        var planned = run.Operation switch
        {
            DeploymentRunOperation.Provision =>
                await PlanProvisionAsync(application, deployment, scoreFile, inputs, matchedResources,
                    cancellationToken),
            DeploymentRunOperation.Destroy => await PlanDestroyAsync(inputs, removableInstances, cancellationToken),
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

    private static Dictionary<string, Resource> MatchKnownResources(Application application, ScoreFile scoreFile,
        IReadOnlyList<Resource> knownResources)
    {
        var keys = scoreFile.Resources!.Keys.ToHashSet(StringComparer.Ordinal);
        var claimedPreviousKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var matches = new Dictionary<string, Resource>();

        foreach (var (key, scoreResource) in scoreFile.Resources)
        {
            var previousKeys = scoreResource.GetPreviousKeys();

            foreach (var previousKey in previousKeys)
            {
                if (keys.Contains(previousKey))
                {
                    throw Invalid(
                        $"Resource '{key}' lists previous key '{previousKey}', which is still a resource in the score file.");
                }

                if (!claimedPreviousKeys.TryAdd(previousKey, key))
                {
                    throw Invalid(
                        $"Previous key '{previousKey}' is listed by both resource '{claimedPreviousKeys[previousKey]}' and resource '{key}'.");
                }
            }

            var slug = Resource.CreateSlug(ResourceName(application, key, scoreResource));
            var current = knownResources.FirstOrDefault(r => r.Slug == slug);
            var renamed = scoreResource.Id is null
                ? previousKeys
                    .Select(previousKey => Resource.CreateSlug(ResourceName(application, previousKey, scoreResource)))
                    .Distinct()
                    .Where(previousSlug => previousSlug != slug)
                    .Select(previousSlug => knownResources.FirstOrDefault(r => r.Slug == previousSlug))
                    .OfType<Resource>()
                    .ToList()
                : [];

            if (renamed.Count > 0 && current is not null)
            {
                throw Invalid(
                    $"Resource '{key}' lists a previous key of existing resource '{renamed[0].Slug}', but resource '{slug}' also exists.");
            }

            if (renamed.Count > 1)
            {
                throw Invalid($"The previous keys of resource '{key}' match more than one existing resource.");
            }

            if ((current ?? renamed.FirstOrDefault()) is { } match)
            {
                matches[slug] = match;
            }
        }

        return matches;
    }

    private async Task<Dictionary<string, List<ResourceInstance>>> GetRemovableInstancesAsync(
        Dictionary<string, Resource> matchedResources, CancellationToken cancellationToken)
    {
        var removableInstances = new Dictionary<string, List<ResourceInstance>>();

        foreach (var (slug, resource) in matchedResources)
        {
            var instances = (await _resourceInstanceRepository.GetByResourceAsync(resource.Id, cancellationToken))
                .Where(i => i.Status != ResourceInstanceStatus.Removed)
                .ToList();

            if (instances.Count > 0)
            {
                removableInstances[slug] = instances;
            }
        }

        return removableInstances;
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
                      $"Resource '{slug}' already exists with a different resource template (resource '{key}').")
                : template.GetLatestVersion()
                  ?? throw Invalid($"Resource template '{template.Type}' has no active version (resource '{key}').");
            var previousKeys = resource.GetPreviousKeys();

            var input = new RunInput(
                Key: key,
                TemplateName: template.Name,
                TemplateType: template.Type,
                Provider: ToRunInputProvider(template.Provider),
                Source: new RunInputSource(version.Source.BaseUrl, version.Source.Tag,
                    string.IsNullOrEmpty(version.Source.FolderPath) ? null : version.Source.FolderPath),
                Parameters: parameters,
                PreviousKeys: previousKeys.Count > 0 ? previousKeys : null);

            inputs.Add(new ResolvedInput(input, template, version, resource, name, slug));
        }

        ValidateReferences(inputs);

        return inputs;
    }

    private static void ValidateReferences(List<ResolvedInput> inputs)
    {
        var providers = inputs.ToDictionary(i => i.Input.Key, i => i.Input.Provider);

        foreach (var input in inputs.Select(i => i.Input))
        {
            foreach (var (name, value) in input.Parameters)
            {
                if (ScoreReference.HasMalformed(value))
                {
                    throw Invalid(
                        $"Parameter '{name}' of resource '{input.Key}' has a malformed reference; expected ${{resources.<key>.<output>}}.");
                }

                foreach (var reference in ScoreReference.Find(value))
                {
                    if (reference.Key == input.Key)
                    {
                        throw Invalid($"Parameter '{name}' of resource '{input.Key}' references itself.");
                    }

                    if (!providers.TryGetValue(reference.Key, out var provider))
                    {
                        throw Invalid(
                            $"Parameter '{name}' of resource '{input.Key}' references resource '{reference.Key}', which is not in the score file.");
                    }

                    if (input.Provider != RunInputProvider.Terraform || provider != RunInputProvider.Terraform)
                    {
                        throw Invalid(
                            $"Parameter '{name}' of resource '{input.Key}' references resource '{reference.Key}', but outputs can only be referenced between Terraform resources.");
                    }
                }
            }
        }
    }

    private async Task<List<PlannedResourceInstance>> PlanProvisionAsync(Application application,
        Deployment deployment, ScoreFile scoreFile, List<ResolvedInput> inputs,
        Dictionary<string, Resource> matchedResources, CancellationToken cancellationToken)
    {
        EnsureOneTemplatePerResource(inputs, matchedResources);

        var instances = await RecordResourcesAsync(application, deployment, scoreFile, inputs, matchedResources,
            cancellationToken);

        foreach (var (instance, _) in instances)
        {
            BeginProvisioning(instance);
            await _resourceInstanceRepository.UpdateAsync(instance, cancellationToken);
        }

        return instances.Select(i => new PlannedResourceInstance(i.Instance.Id, i.Key)).ToList();
    }

    private async Task<List<PlannedResourceInstance>> PlanDestroyAsync(List<ResolvedInput> inputs,
        Dictionary<string, List<ResourceInstance>> removableInstances, CancellationToken cancellationToken)
    {
        var keysBySlug = inputs.GroupBy(i => i.Slug).ToDictionary(g => g.Key, g => g.First().Input.Key);
        var planned = new List<PlannedResourceInstance>();

        foreach (var (slug, instances) in removableInstances)
        {
            foreach (var instance in instances)
            {
                BeginRemoval(instance);
                await _resourceInstanceRepository.UpdateAsync(instance, cancellationToken);
                planned.Add(new PlannedResourceInstance(instance.Id, keysBySlug[slug]));
            }
        }

        return planned;
    }

    private static void EnsureOneTemplatePerResource(List<ResolvedInput> inputs,
        Dictionary<string, Resource> matchedResources)
    {
        foreach (var input in inputs)
        {
            var templateId = matchedResources.GetValueOrDefault(input.Slug)?.ResourceTemplateId
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
        Dictionary<string, Resource> matchedResources, CancellationToken cancellationToken)
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
            var resource = await GetOrCreateResourceAsync(application, deployment, input, matchedResources,
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

            try
            {
                graph.SetDependencies(keys.Key, dependencies);
            }
            catch (InvalidOperationException exception)
            {
                throw Invalid(
                    $"Resource '{keys.First()}' cannot depend on the resources it references: {exception.Message}");
            }
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
        ResolvedInput input, Dictionary<string, Resource> matchedResources, CancellationToken cancellationToken)
    {
        if (matchedResources.GetValueOrDefault(input.Slug) is { } existing)
        {
            if (existing.Slug != input.Slug)
            {
                _logger.LogInformation("Renaming resource {ResourceId} from {PreviousSlug} to {Slug} (resource {Key}).",
                    existing.Id.Value, existing.Slug, input.Slug, input.Input.Key);
                existing.Rename(input.Name);
                await _resourceRepository.UpdateAsync(existing, cancellationToken);
            }

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
        matchedResources[input.Slug] = created;
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
        .SelectMany(p => ScoreReference.Find(p.Value))
        .Select(r => r.Key)
        .Distinct();

    private sealed record ResolvedInput(
        RunInput Input,
        ResourceTemplate Template,
        ResourceTemplateVersion Version,
        ScoreResource ScoreResource,
        string Name,
        string Slug);
}
