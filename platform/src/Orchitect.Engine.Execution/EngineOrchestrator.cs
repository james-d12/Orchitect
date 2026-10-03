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
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Provisioner;

namespace Orchitect.Engine.Execution;

public interface IEngineOrchestrator
{
    /// <summary>
    /// Provisions a deployment's resources, recording each as a resource, an instance and a node in the
    /// environment's dependency graph, and records the instances on the run, which settles them when it completes.
    /// </summary>
    Task StartAsync(Application application, Deployment deployment, DeploymentRunId runId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Destroys a deployment's resources using the same project and state as <see cref="StartAsync"/>, and records
    /// the instances being removed on the run, which settles them and releases the resources when it completes.
    /// </summary>
    Task DestroyAsync(Application application, Deployment deployment, DeploymentRunId runId,
        CancellationToken cancellationToken);
}

public sealed partial class EngineOrchestrator : IEngineOrchestrator
{
    private readonly ILogger<EngineOrchestrator> _logger;
    private readonly IResourceTemplateRepository _resourceTemplateRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceInstanceRepository _resourceInstanceRepository;
    private readonly IResourceDependencyGraphRepository _resourceDependencyGraphRepository;
    private readonly IDeploymentRunRepository _deploymentRunRepository;
    private readonly IEngineProvisioner _engineProvisioner;
    private readonly IScoreDriver _scoreDriver;

    public EngineOrchestrator(ILogger<EngineOrchestrator> logger, IScoreDriver scoreDriver,
        IResourceTemplateRepository resourceTemplateRepository, IResourceRepository resourceRepository,
        IResourceInstanceRepository resourceInstanceRepository,
        IResourceDependencyGraphRepository resourceDependencyGraphRepository,
        IDeploymentRunRepository deploymentRunRepository, IEngineProvisioner engineProvisioner)
    {
        _logger = logger;
        _scoreDriver = scoreDriver;
        _resourceTemplateRepository = resourceTemplateRepository;
        _resourceRepository = resourceRepository;
        _resourceInstanceRepository = resourceInstanceRepository;
        _resourceDependencyGraphRepository = resourceDependencyGraphRepository;
        _deploymentRunRepository = deploymentRunRepository;
        _engineProvisioner = engineProvisioner;
    }

    public async Task StartAsync(Application application, Deployment deployment, DeploymentRunId runId,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        try
        {
            var provisionRequest = await BuildProvisionInputsAsync(application, deployment, cancellationToken);

            var instances = await RecordResourcesAsync(application, deployment, provisionRequest.ScoreFile,
                provisionRequest.Inputs, cancellationToken);

            await RecordPlanAsync(runId, provisionRequest.Context, instances, cancellationToken);
            await TransitionAsync(instances, BeginProvisioning, cancellationToken);

            _logger.LogInformation("Provisioning Resources for score file");

            await _engineProvisioner.ProvisionAsync(provisionRequest.Inputs, provisionRequest.Context,
                cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(exception, "An error occured while provisioning the score file.");
            throw;
        }
    }

    public async Task DestroyAsync(Application application, Deployment deployment, DeploymentRunId runId,
        CancellationToken cancellationToken)
    {
        using var activity = Tracing.StartActivity();

        try
        {
            var provisionRequest = await BuildProvisionInputsAsync(application, deployment, cancellationToken);

            var resources = await FindRecordedResourcesAsync(application, deployment, provisionRequest.ScoreFile,
                provisionRequest.Inputs, cancellationToken);
            var instances = await FindRemovableInstancesAsync(resources, cancellationToken);

            await RecordPlanAsync(runId, provisionRequest.Context, instances, cancellationToken);
            await TransitionAsync(instances, BeginRemoval, cancellationToken);

            _logger.LogInformation("Destroying Resources for score file");

            await _engineProvisioner.DeleteAsync(provisionRequest.Inputs, provisionRequest.Context,
                cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.RecordException(exception);
            _logger.LogError(exception, "An error occured while destroying the score file.");
            throw;
        }
    }

    private async Task<(ProvisionContext Context, List<ProvisionInput> Inputs, ScoreFile ScoreFile)>
        BuildProvisionInputsAsync(Application application, Deployment deployment, CancellationToken cancellationToken)
    {
        ScoreFile scoreFile = await _scoreDriver.ParseAsync(deployment, application, cancellationToken)
                              ?? throw new InvalidOperationException("Unable to find or parse the score file.");

        if (scoreFile.Resources is null || scoreFile.Resources.Count == 0)
        {
            throw new InvalidOperationException("There are no resources in the score file.");
        }

        var context = new ProvisionContext(
            ProjectName: scoreFile.Metadata.Name,
            ApplicationId: deployment.ApplicationId.Value.ToString(),
            EnvironmentId: deployment.EnvironmentId.Value.ToString());

        var provisionInputs = new List<ProvisionInput>();

        foreach (var resource in scoreFile.Resources)
        {
            var type = resource.Value.Type.Trim().ToLower();
            var inputs = resource.Value.Parameters ?? [];

            ResourceTemplate resourceTemplate =
                await _resourceTemplateRepository.GetByTypeAsync(type, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"No resource template found for type '{type}' (resource '{resource.Key}').");

            provisionInputs.Add(new ProvisionInput(resourceTemplate, inputs, resource.Key));
        }

        return (context, provisionInputs, scoreFile);
    }

    private async Task<List<ResourceInstance>> RecordResourcesAsync(Application application, Deployment deployment,
        ScoreFile scoreFile, List<ProvisionInput> inputs, CancellationToken cancellationToken)
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
            var scoreResource = scoreFile.Resources![input.Key];
            var version = input.Template.GetLatestVersion()
                          ?? throw new InvalidOperationException(
                              $"Resource template '{input.Template.Type}' has no active version (resource '{input.Key}').");

            var resource = await GetOrCreateResourceAsync(application, deployment, input, scoreResource,
                knownResources, cancellationToken);
            graph.AddResource(resource.Id);
            resourcesByKey[input.Key] = resource;

            if (!instancesByResource.ContainsKey(resource.Id))
            {
                instancesByResource[resource.Id] =
                    await GetOrCreateInstanceAsync(resource, version, input, cancellationToken);
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
        ProvisionInput input, ScoreResource scoreResource, List<Resource> knownResources,
        CancellationToken cancellationToken)
    {
        var name = ResolveResourceName(application, input.Key, scoreResource);
        var slug = Resource.CreateSlug(name);
        var existing = knownResources.FirstOrDefault(r => r.Slug == slug);

        if (existing is not null)
        {
            if (existing.ResourceTemplateId != input.Template.Id)
            {
                throw new InvalidOperationException(
                    $"Resource '{slug}' already exists with a different resource template (resource '{input.Key}').");
            }

            return existing;
        }

        var resource = Resource.Create(new CreateResourceRequest(
            OrganisationId: application.OrganisationId,
            Name: name,
            Description: scoreResource.Metadata?.Annotations?.GetValueOrDefault("description") ?? string.Empty,
            ResourceTemplateId: input.Template.Id,
            EnvironmentId: deployment.EnvironmentId,
            Kind: Enum.TryParse<ResourceKind>(scoreResource.Class, ignoreCase: true, out var kind)
                ? kind
                : ResourceKind.Direct,
            ApplicationId: application.Id));

        var created = await _resourceRepository.CreateAsync(resource, cancellationToken)
                      ?? throw new InvalidOperationException($"Unable to create resource '{slug}'.");
        knownResources.Add(created);
        return created;
    }

    private async Task<ResourceInstance> GetOrCreateInstanceAsync(Resource resource, ResourceTemplateVersion version,
        ProvisionInput input, CancellationToken cancellationToken)
    {
        var inputParameters = input.Inputs.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));

        var existingInstances = await _resourceInstanceRepository.GetByResourceAsync(resource.Id, cancellationToken);
        var current = existingInstances
            .Where(i => i.Status != ResourceInstanceStatus.Removed)
            .MaxBy(i => i.CreatedAt);

        if (current is not null)
        {
            current.Reconfigure(version.Id, inputParameters);
            return current;
        }

        var instance = ResourceInstance.Create(new CreateResourceInstanceRequest(
            ResourceId: resource.Id,
            OrganisationId: resource.OrganisationId,
            Name: $"{resource.Slug}-instance",
            TemplateVersionId: version.Id,
            EnvironmentId: resource.EnvironmentId,
            InputParameters: inputParameters));

        return await _resourceInstanceRepository.CreateAsync(instance, cancellationToken)
               ?? throw new InvalidOperationException($"Unable to create an instance of resource '{resource.Slug}'.");
    }

    private async Task<List<Resource>> FindRecordedResourcesAsync(Application application, Deployment deployment,
        ScoreFile scoreFile, List<ProvisionInput> inputs, CancellationToken cancellationToken)
    {
        var existingResources =
            await _resourceRepository.GetByEnvironmentAsync(deployment.EnvironmentId, cancellationToken);
        var slugs = inputs
            .Select(i => Resource.CreateSlug(ResolveResourceName(application, i.Key, scoreFile.Resources![i.Key])))
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

    private async Task RecordPlanAsync(DeploymentRunId runId, ProvisionContext context,
        List<ResourceInstance> instances, CancellationToken cancellationToken)
    {
        var run = await _deploymentRunRepository.GetByIdAsync(runId, cancellationToken)
                  ?? throw new InvalidOperationException($"Run '{runId.Value}' was not found.");

        await _deploymentRunRepository.UpdateAsync(
            run.RecordPlan(context.ProjectName, instances.Select(i => i.Id).ToList()), cancellationToken);
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

    private static string ResolveResourceName(Application application, string key, ScoreResource scoreResource) =>
        scoreResource.Id ?? $"{application.Name}-{key}";

    private static IEnumerable<string> FindReferencedResourceKeys(ScoreResource scoreResource) =>
        (scoreResource.Parameters ?? [])
        .SelectMany(p => ResourceReferenceRegex().Matches(p.Value))
        .Select(m => m.Groups["key"].Value)
        .Distinct();

    [GeneratedRegex(@"\$\{resources\.(?<key>[A-Za-z0-9_-]+)\.")]
    private static partial Regex ResourceReferenceRegex();
}
