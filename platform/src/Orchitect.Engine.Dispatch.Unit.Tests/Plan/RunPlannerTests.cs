using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Dispatch.Plan;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Plan;

public sealed class RunPlannerTests
{
    private const string StorageType = "azure-storage-account";
    private const string KeyVaultType = "azure-key-vault";
    private const string InactiveType = "inactive-template";

    private readonly Dictionary<DeploymentRunId, DeploymentRun> _runs = [];
    private readonly InMemoryPlanRepository _plans = new();
    private readonly InMemoryResourceRepository _resources = new();
    private readonly InMemoryResourceInstanceRepository _instances = new();
    private readonly InMemoryResourceDependencyGraphRepository _graphs = new();
    private readonly IDeploymentRunRepository _runRepository = Substitute.For<IDeploymentRunRepository>();
    private readonly IDeploymentRepository _deploymentRepository = Substitute.For<IDeploymentRepository>();
    private readonly IApplicationRepository _applicationRepository = Substitute.For<IApplicationRepository>();
    private readonly IResourceTemplateRepository _templateRepository = Substitute.For<IResourceTemplateRepository>();
    private readonly Application _application;
    private readonly Deployment _deployment;

    public RunPlannerTests()
    {
        _application = Application.Create("orders", new Repository
        {
            Name = "orders",
            Url = new Uri("https://example.com/orders.git"),
            Provider = RepositoryProvider.GitHub
        }, new OrganisationId());
        _deployment = Deployment.Create(_application.Id, new EnvironmentId(Guid.NewGuid()),
            new CommitId(new string('a', 40)), "test@example.com");

        _runRepository.GetByIdAsync(Arg.Any<DeploymentRunId>(), Arg.Any<CancellationToken>())
            .Returns(call => _runs.GetValueOrDefault(call.Arg<DeploymentRunId>()));
        _deploymentRepository.GetByIdAsync(_deployment.Id, Arg.Any<CancellationToken>()).Returns(_deployment);
        _applicationRepository.GetByIdAsync(_application.Id, Arg.Any<CancellationToken>()).Returns(_application);

        var templates = new[]
        {
            NewTemplate(StorageType, ResourceTemplateVersionState.Active),
            NewTemplate(KeyVaultType, ResourceTemplateVersionState.Active),
            NewTemplate(InactiveType, ResourceTemplateVersionState.Inactive)
        }.ToDictionary(t => t.Type);
        _templateRepository.GetByTypeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => templates.GetValueOrDefault(call.Arg<string>()));
    }

    [Fact]
    public async Task PlanAsync_ResourceTemplateMissing_ThrowsWithoutRecording()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            PlanAsync(DeploymentRunOperation.Provision, Score(("storage", "unknown-type", new() { ["name"] = "x" }))));

        Assert.Contains("unknown-type", exception.Message);
        Assert.Contains("storage", exception.Message);
        Assert.Empty(_resources.Items);
        Assert.Empty(_plans.Items);
    }

    [Fact]
    public async Task PlanAsync_TemplateHasNoActiveVersion_ThrowsWithoutRecording()
    {
        var exception = await Assert.ThrowsAsync<RunPlanException>(() =>
            PlanAsync(DeploymentRunOperation.Provision, Score(("storage", InactiveType, null))));

        Assert.Contains("no active version", exception.Message);
        Assert.Empty(_resources.Items);
    }

    [Fact]
    public async Task PlanAsync_NoResources_Throws()
    {
        await Assert.ThrowsAsync<RunPlanException>(() => PlanAsync(DeploymentRunOperation.Destroy, Score()));
    }

    [Fact]
    public async Task PlanAsync_RunNotRunning_Throws()
    {
        var run = DeploymentRun.Queue(_deployment.Id, DeploymentRunOperation.Provision);
        _runs[run.Id] = run;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreatePlanner().PlanAsync(run.Id, Score(("storage", StorageType, null)), CancellationToken.None));

        Assert.Empty(_plans.Items);
    }

    [Fact]
    public async Task PlanAsync_Provision_ReturnsContextAndResolvedInputs()
    {
        var (_, plan) = await PlanAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, new() { ["sku"] = "LRS" })));

        Assert.Equal(new RunContext("orders", _deployment.ApplicationId.Value, _deployment.EnvironmentId.Value),
            plan.Context);
        var input = Assert.Single(plan.Inputs);
        Assert.Equal("storage", input.Key);
        Assert.Equal($"{StorageType} template", input.TemplateName);
        Assert.Equal(StorageType, input.TemplateType);
        Assert.Equal(RunInputProvider.Terraform, input.Provider);
        Assert.Equal(new RunInputSource(new Uri("https://example.com/modules.git"), "v1.0.0", StorageType),
            input.Source);
        Assert.Equal("LRS", input.Parameters["sku"]);
    }

    [Fact]
    public async Task PlanAsync_ParametersMissing_PlansEmptyParameters()
    {
        var (_, plan) = await PlanAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)));

        Assert.Empty(Assert.Single(plan.Inputs).Parameters);
    }

    [Fact]
    public async Task PlanAsync_Provision_RecordsResourceInstanceAndGraphNodeAsProvisioning()
    {
        await PlanAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, new() { ["sku"] = "LRS" })));

        var resource = Assert.Single(_resources.Items);
        Assert.Equal("orders-storage", resource.Slug);
        Assert.Equal(_deployment.EnvironmentId, resource.EnvironmentId);
        Assert.Equal(_application.Id, resource.ApplicationId);
        Assert.Equal(_application.OrganisationId, resource.OrganisationId);
        Assert.Equal([_application.Id], resource.Consumers);

        var instance = Assert.Single(_instances.Items);
        Assert.Equal(resource.Id, instance.ResourceId);
        Assert.Equal(ResourceInstanceStatus.Provisioning, instance.Status);
        Assert.Equal("LRS", instance.InputParameters["sku"].GetString());

        Assert.True(Assert.Single(_graphs.Items).ContainsResource(resource.Id));
    }

    [Fact]
    public async Task PlanAsync_CalledAgainForTheSameRun_ReturnsStoredPlanWithoutRecordingAgain()
    {
        var (runId, first) = await PlanAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, new() { ["sku"] = "LRS" })));
        var instance = Assert.Single(_instances.Items);
        var updatedAt = instance.UpdatedAt;

        var second = await CreatePlanner().PlanAsync(runId,
            Score(("storage", StorageType, new() { ["sku"] = "GRS" })), CancellationToken.None);

        Assert.Equal(first.Context, second.Context);
        Assert.Equal("LRS", Assert.Single(second.Inputs).Parameters["sku"]);
        Assert.Single(_plans.Items);
        Assert.Single(_resources.Items);
        Assert.Equal(updatedAt, Assert.Single(_instances.Items).UpdatedAt);
        Assert.Equal("LRS", instance.InputParameters["sku"].GetString());
    }

    [Fact]
    public async Task FinishAsync_ProvisionSucceeds_MarksInstancesActiveWithOutput()
    {
        await RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)), RunOutcome.Succeeded);

        var instance = Assert.Single(_instances.Items);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        Assert.Equal("orders", instance.Output!.Workspace);
        Assert.Equal(new Uri("https://example.com/modules.git"), instance.Output.Location);
    }

    [Fact]
    public async Task FinishAsync_ProvisionFails_MarksInstancesFailed()
    {
        await RunAsync(DeploymentRunOperation.Provision,
            Score(("storage", StorageType, null), ("vault", KeyVaultType, null)), RunOutcome.Failed);

        Assert.Equal(2, _instances.Items.Count);
        Assert.All(_instances.Items, i => Assert.Equal(ResourceInstanceStatus.Failed, i.Status));
    }

    [Fact]
    public async Task FinishAsync_CalledAgain_LeavesFinishedInstancesAlone()
    {
        var runId = await RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, null)),
            RunOutcome.Succeeded);

        await CreatePlanner().FinishAsync(runId, RunOutcome.Failed, CancellationToken.None);

        Assert.Equal(ResourceInstanceStatus.Active, Assert.Single(_instances.Items).Status);
    }

    [Fact]
    public async Task FinishAsync_RunNotPlanned_DoesNothing()
    {
        var run = DeploymentRun.Queue(_deployment.Id, DeploymentRunOperation.Provision).Start();
        _runs[run.Id] = run;

        await CreatePlanner().FinishAsync(run.Id, RunOutcome.Failed, CancellationToken.None);

        Assert.Empty(_instances.Items);
        Assert.Empty(_resources.Items);
    }

    [Fact]
    public async Task PlanAsync_Redeploy_ReusesResourceAndReconfiguresInstance()
    {
        await RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, new() { ["sku"] = "LRS" })),
            RunOutcome.Succeeded);

        await RunAsync(DeploymentRunOperation.Provision, Score(("storage", StorageType, new() { ["sku"] = "GRS" })),
            RunOutcome.Succeeded);

        Assert.Single(_resources.Items);
        Assert.Single(_graphs.Items);
        var instance = Assert.Single(_instances.Items);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        Assert.Equal("GRS", instance.InputParameters["sku"].GetString());
    }

    [Fact]
    public async Task PlanAsync_RetryAfterFailure_ProvisionsSameInstance()
    {
        var score = Score(("storage", StorageType, null));
        await RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Failed);

        await RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        Assert.Equal(ResourceInstanceStatus.Active, Assert.Single(_instances.Items).Status);
    }

    [Fact]
    public async Task PlanAsync_ParameterReferencesResource_AddsDependency()
    {
        await PlanAsync(DeploymentRunOperation.Provision, Score(
            ("vault", KeyVaultType, null),
            ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" })));

        var storage = _resources.Items.Single(r => r.Slug == "orders-storage");
        var vault = _resources.Items.Single(r => r.Slug == "orders-vault");
        var graph = Assert.Single(_graphs.Items);
        Assert.True(graph.HasDependencyPath(storage.Id, vault.Id));
        Assert.False(graph.HasDependencyPath(vault.Id, storage.Id));
    }

    [Fact]
    public async Task PlanAsync_ReferenceRemovedFromScore_RemovesDependency()
    {
        await RunAsync(DeploymentRunOperation.Provision, Score(
                ("vault", KeyVaultType, null),
                ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" })),
            RunOutcome.Succeeded);

        await RunAsync(DeploymentRunOperation.Provision,
            Score(("vault", KeyVaultType, null), ("storage", StorageType, null)), RunOutcome.Succeeded);

        var storage = _resources.Items.Single(r => r.Slug == "orders-storage");
        Assert.Equal(0, Assert.Single(_graphs.Items).DependencyCount(storage.Id));
    }

    [Fact]
    public async Task PlanAsync_ScoreResourceHasId_UsesIdAsResourceName()
    {
        var scoreFile = Score(("storage", StorageType, null));
        scoreFile.Resources!["storage"] = scoreFile.Resources["storage"] with { Id = "shared-storage" };

        await PlanAsync(DeploymentRunOperation.Provision, scoreFile);

        Assert.Equal("shared-storage", Assert.Single(_resources.Items).Slug);
    }

    [Fact]
    public async Task PlanAsync_Destroy_MovesRecordedInstancesToRemoving()
    {
        var score = Score(("storage", StorageType, null), ("vault", KeyVaultType, null));
        await RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        await PlanAsync(DeploymentRunOperation.Destroy, score);

        Assert.All(_instances.Items, i => Assert.Equal(ResourceInstanceStatus.Removing, i.Status));
    }

    [Fact]
    public async Task FinishAsync_DestroySucceeds_MarksInstancesRemovedAndReleasesResources()
    {
        var score = Score(
            ("vault", KeyVaultType, null),
            ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" }));
        await RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        await RunAsync(DeploymentRunOperation.Destroy, score, RunOutcome.Succeeded);

        var graph = Assert.Single(_graphs.Items);
        Assert.All(_instances.Items, i => Assert.Equal(ResourceInstanceStatus.Removed, i.Status));
        Assert.Equal(2, _resources.Items.Count);
        Assert.All(_resources.Items, r => Assert.Empty(r.Consumers));
        Assert.All(_resources.Items, r => Assert.False(graph.ContainsResource(r.Id)));
    }

    [Fact]
    public async Task FinishAsync_DestroyAfterFailedProvision_MarksInstancesRemoved()
    {
        var score = Score(("storage", StorageType, null));
        await RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Failed);

        await RunAsync(DeploymentRunOperation.Destroy, score, RunOutcome.Succeeded);

        Assert.Equal(ResourceInstanceStatus.Removed, Assert.Single(_instances.Items).Status);
    }

    [Fact]
    public async Task FinishAsync_DestroyFails_MarksRemovalFailedAndKeepsConsumerAndGraphNode()
    {
        var score = Score(("storage", StorageType, null));
        await RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        await RunAsync(DeploymentRunOperation.Destroy, score, RunOutcome.Failed);

        var resource = Assert.Single(_resources.Items);
        Assert.Equal(ResourceInstanceStatus.RemovalFailed, Assert.Single(_instances.Items).Status);
        Assert.Equal([_application.Id], resource.Consumers);
        Assert.True(Assert.Single(_graphs.Items).ContainsResource(resource.Id));
    }

    [Fact]
    public async Task PlanAsync_DestroyWithNothingRecorded_PlansInputsWithoutCreatingRecords()
    {
        var (_, plan) = await PlanAsync(DeploymentRunOperation.Destroy, Score(("storage", StorageType, null)));

        Assert.Single(plan.Inputs);
        Assert.Empty(_resources.Items);
        Assert.Empty(_instances.Items);
    }

    private RunPlanner CreatePlanner() =>
        new(NullLogger<RunPlanner>.Instance, _runRepository, _plans, _deploymentRepository, _applicationRepository,
            _templateRepository, _resources, _instances, _graphs);

    private async Task<(DeploymentRunId RunId, RunPlan Plan)> PlanAsync(DeploymentRunOperation operation,
        ScoreFile scoreFile)
    {
        var run = DeploymentRun.Queue(_deployment.Id, operation).Start();
        _runs[run.Id] = run;

        return (run.Id, await CreatePlanner().PlanAsync(run.Id, scoreFile, CancellationToken.None));
    }

    private async Task<DeploymentRunId> RunAsync(DeploymentRunOperation operation, ScoreFile scoreFile,
        RunOutcome outcome)
    {
        var (runId, _) = await PlanAsync(operation, scoreFile);
        await CreatePlanner().FinishAsync(runId, outcome, CancellationToken.None);
        return runId;
    }

    private static ScoreFile Score(params (string Key, string Type, Dictionary<string, string>? Parameters)[] resources) =>
        new()
        {
            ApiVersion = "score.dev/v1b1",
            Metadata = new ScoreMetadata { Name = "orders" },
            Resources = resources.ToDictionary(r => r.Key,
                r => new ScoreResource { Type = r.Type, Parameters = r.Parameters })
        };

    private static ResourceTemplate NewTemplate(string type, ResourceTemplateVersionState state) =>
        ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
        {
            OrganisationId = new OrganisationId(),
            Name = $"{type} template",
            Type = type,
            Description = $"A {type}.",
            Provider = ResourceTemplateProvider.Terraform,
            Version = "1.0.0",
            Source = new ResourceTemplateVersionSource
            {
                BaseUrl = new Uri("https://example.com/modules.git"),
                FolderPath = type,
                Tag = "v1.0.0"
            },
            Notes = "Initial version.",
            State = state
        });

    private sealed class InMemoryPlanRepository : IDeploymentRunPlanRepository
    {
        public List<DeploymentRunPlan> Items { get; } = [];

        public Task<DeploymentRunPlan?> GetByRunIdAsync(DeploymentRunId runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(p => p.RunId == runId));

        public Task<DeploymentRunPlan?> CreateAsync(DeploymentRunPlan plan,
            CancellationToken cancellationToken = default)
        {
            if (Items.Any(p => p.RunId == plan.RunId))
            {
                throw new InvalidOperationException($"Run '{plan.RunId.Value}' already has a plan.");
            }

            Items.Add(plan);
            return Task.FromResult<DeploymentRunPlan?>(plan);
        }
    }

    private sealed class InMemoryResourceRepository : IResourceRepository
    {
        public List<Resource> Items { get; } = [];

        public Task<Resource?> CreateAsync(Resource environment, CancellationToken cancellationToken = default)
        {
            Items.Add(environment);
            return Task.FromResult<Resource?>(environment);
        }

        public IEnumerable<Resource> GetAll() => Items;

        public Task<Resource?> GetByIdAsync(ResourceId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

        public Task<Resource?> UpdateAsync(Resource resource, CancellationToken cancellationToken = default) =>
            Task.FromResult<Resource?>(resource);

        public Task<bool> DeleteAsync(ResourceId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.RemoveAll(r => r.Id == id) > 0);

        public Task<IReadOnlyList<Resource>> GetByEnvironmentAsync(EnvironmentId environmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Resource>>(Items.Where(r => r.EnvironmentId == environmentId).ToList());
    }

    private sealed class InMemoryResourceInstanceRepository : IResourceInstanceRepository
    {
        public List<ResourceInstance> Items { get; } = [];

        public Task<ResourceInstance?> CreateAsync(ResourceInstance environment,
            CancellationToken cancellationToken = default)
        {
            Items.Add(environment);
            return Task.FromResult<ResourceInstance?>(environment);
        }

        public IEnumerable<ResourceInstance> GetAll() => Items;

        public Task<ResourceInstance?> GetByIdAsync(ResourceInstanceId id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

        public Task<ResourceInstance?> UpdateAsync(ResourceInstance instance,
            CancellationToken cancellationToken = default) => Task.FromResult<ResourceInstance?>(instance);

        public Task<bool> DeleteAsync(ResourceInstanceId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.RemoveAll(i => i.Id == id) > 0);

        public Task<IReadOnlyList<ResourceInstance>> GetByResourceAsync(ResourceId resourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceInstance>>(Items.Where(i => i.ResourceId == resourceId).ToList());

        public Task<IReadOnlyList<ResourceInstance>> GetByEnvironmentAsync(EnvironmentId environmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceInstance>>(Items.Where(i => i.EnvironmentId == environmentId)
                .ToList());
    }

    private sealed class InMemoryResourceDependencyGraphRepository : IResourceDependencyGraphRepository
    {
        public List<ResourceDependencyGraph> Items { get; } = [];

        public Task<ResourceDependencyGraph?> CreateAsync(ResourceDependencyGraph environment,
            CancellationToken cancellationToken = default)
        {
            Items.Add(environment);
            return Task.FromResult<ResourceDependencyGraph?>(environment);
        }

        public IEnumerable<ResourceDependencyGraph> GetAll() => Items;

        public Task<ResourceDependencyGraph?> GetByIdAsync(ResourceDependencyGraphId id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(g => g.Id == id));

        public Task<ResourceDependencyGraph?> GetByEnvironmentAsync(EnvironmentId environmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(g => g.EnvironmentId == environmentId));

        public Task<ResourceDependencyGraph?> UpdateAsync(ResourceDependencyGraph graph,
            CancellationToken cancellationToken = default) => Task.FromResult<ResourceDependencyGraph?>(graph);
    }
}
