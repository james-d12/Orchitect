using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Provisioner;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Engine.Execution.Unit.Tests;

public sealed class EngineOrchestratorTests
{
    private const string StorageType = "azure-storage-account";
    private const string KeyVaultType = "azure-key-vault";
    private const string InactiveType = "inactive-template";

    private readonly RecordingProvisioner _provisioner = new();
    private readonly InMemoryResourceRepository _resources = new();
    private readonly InMemoryResourceInstanceRepository _instances = new();
    private readonly InMemoryResourceDependencyGraphRepository _graphs = new();
    private readonly Application _application = NewApplication();
    private readonly Deployment _deployment = NewDeployment();

    [Fact]
    public async Task StartAsync_ResourceTemplateMissing_ThrowsWithoutProvisioning()
    {
        var orchestrator = CreateOrchestrator(Score(("storage", "unknown-type", new() { ["name"] = "x" })));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync(_application, _deployment, CancellationToken.None));

        Assert.Contains("unknown-type", exception.Message);
        Assert.Contains("storage", exception.Message);
        Assert.Null(_provisioner.Inputs);
    }

    [Fact]
    public async Task StartAsync_ParametersMissing_ProvisionsWithEmptyInputs()
    {
        var orchestrator = CreateOrchestrator(Score(("storage", StorageType, null)));

        await orchestrator.StartAsync(_application, _deployment, CancellationToken.None);

        var input = Assert.Single(_provisioner.Inputs!);
        Assert.Empty(input.Inputs);
    }

    [Fact]
    public async Task StartAsync_ScoreFileMissing_Throws()
    {
        var orchestrator = CreateOrchestrator(null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync(_application, _deployment, CancellationToken.None));
    }

    [Fact]
    public async Task DestroyAsync_NoResources_Throws()
    {
        var orchestrator = CreateOrchestrator(Score());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.DestroyAsync(_application, _deployment, CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_TemplateHasNoActiveVersion_ThrowsWithoutProvisioning()
    {
        var orchestrator = CreateOrchestrator(Score(("storage", InactiveType, null)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync(_application, _deployment, CancellationToken.None));

        Assert.Contains("no active version", exception.Message);
        Assert.Null(_provisioner.Inputs);
    }

    [Fact]
    public async Task StartAsync_ProvisionSucceeds_RecordsResourceInstanceAndGraphNode()
    {
        var orchestrator = CreateOrchestrator(Score(("storage", StorageType, new() { ["sku"] = "LRS" })));

        await orchestrator.StartAsync(_application, _deployment, CancellationToken.None);

        var resource = Assert.Single(_resources.Items);
        Assert.Equal("orders-storage", resource.Slug);
        Assert.Equal(_deployment.EnvironmentId, resource.EnvironmentId);
        Assert.Equal(_application.Id, resource.ApplicationId);
        Assert.Equal(_application.OrganisationId, resource.OrganisationId);

        var instance = Assert.Single(_instances.Items);
        Assert.Equal(resource.Id, instance.ResourceId);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        Assert.Equal("LRS", instance.InputParameters["sku"].GetString());
        Assert.Equal("orders", instance.Output!.Workspace);

        var graph = Assert.Single(_graphs.Items);
        Assert.True(graph.ContainsResource(resource.Id));
    }

    [Fact]
    public async Task StartAsync_ProvisionFails_MarksInstancesFailed()
    {
        _provisioner.Failure = new InvalidOperationException("terraform apply failed");
        var orchestrator = CreateOrchestrator(Score(("storage", StorageType, null), ("vault", KeyVaultType, null)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync(_application, _deployment, CancellationToken.None));

        Assert.Equal(2, _instances.Items.Count);
        Assert.All(_instances.Items, i => Assert.Equal(ResourceInstanceStatus.Failed, i.Status));
    }

    [Fact]
    public async Task StartAsync_Redeploy_ReusesResourceAndReconfiguresInstance()
    {
        await CreateOrchestrator(Score(("storage", StorageType, new() { ["sku"] = "LRS" })))
            .StartAsync(_application, _deployment, CancellationToken.None);

        await CreateOrchestrator(Score(("storage", StorageType, new() { ["sku"] = "GRS" })))
            .StartAsync(_application, _deployment, CancellationToken.None);

        Assert.Single(_resources.Items);
        Assert.Single(_graphs.Items);
        var instance = Assert.Single(_instances.Items);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        Assert.Equal("GRS", instance.InputParameters["sku"].GetString());
    }

    [Fact]
    public async Task StartAsync_RetryAfterFailure_ProvisionsSameInstance()
    {
        _provisioner.Failure = new InvalidOperationException("terraform apply failed");
        var score = Score(("storage", StorageType, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(score).StartAsync(_application, _deployment, CancellationToken.None));

        _provisioner.Failure = null;
        await CreateOrchestrator(score).StartAsync(_application, _deployment, CancellationToken.None);

        var instance = Assert.Single(_instances.Items);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
    }

    [Fact]
    public async Task StartAsync_ParameterReferencesResource_AddsDependency()
    {
        var orchestrator = CreateOrchestrator(Score(
            ("vault", KeyVaultType, null),
            ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" })));

        await orchestrator.StartAsync(_application, _deployment, CancellationToken.None);

        var storage = _resources.Items.Single(r => r.Slug == "orders-storage");
        var vault = _resources.Items.Single(r => r.Slug == "orders-vault");
        var graph = Assert.Single(_graphs.Items);
        Assert.True(graph.HasDependencyPath(storage.Id, vault.Id));
        Assert.False(graph.HasDependencyPath(vault.Id, storage.Id));
    }

    [Fact]
    public async Task StartAsync_ScoreResourceHasId_UsesIdAsResourceName()
    {
        var scoreFile = Score(("storage", StorageType, null));
        scoreFile.Resources!["storage"] = scoreFile.Resources["storage"] with { Id = "shared-storage" };

        await CreateOrchestrator(scoreFile).StartAsync(_application, _deployment, CancellationToken.None);

        Assert.Equal("shared-storage", Assert.Single(_resources.Items).Slug);
    }

    [Fact]
    public async Task DestroyAsync_ProvisionedResources_MarksInstancesRemoved()
    {
        var score = Score(("storage", StorageType, null), ("vault", KeyVaultType, null));
        await CreateOrchestrator(score).StartAsync(_application, _deployment, CancellationToken.None);

        await CreateOrchestrator(score).DestroyAsync(_application, _deployment, CancellationToken.None);

        Assert.All(_instances.Items, i => Assert.Equal(ResourceInstanceStatus.Removed, i.Status));
    }

    [Fact]
    public async Task StartAsync_ProvisionSucceeds_RecordsApplicationAsConsumer()
    {
        await CreateOrchestrator(Score(("storage", StorageType, null)))
            .StartAsync(_application, _deployment, CancellationToken.None);

        Assert.Equal([_application.Id], Assert.Single(_resources.Items).Consumers);
    }

    [Fact]
    public async Task StartAsync_ReferenceRemovedFromScore_RemovesDependency()
    {
        await CreateOrchestrator(Score(
                ("vault", KeyVaultType, null),
                ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" })))
            .StartAsync(_application, _deployment, CancellationToken.None);

        await CreateOrchestrator(Score(("vault", KeyVaultType, null), ("storage", StorageType, null)))
            .StartAsync(_application, _deployment, CancellationToken.None);

        var storage = _resources.Items.Single(r => r.Slug == "orders-storage");
        var graph = Assert.Single(_graphs.Items);
        Assert.Equal(0, graph.DependencyCount(storage.Id));
    }

    [Fact]
    public async Task DestroyAsync_Succeeds_ReleasesConsumerAndRemovesGraphNodes()
    {
        var score = Score(
            ("vault", KeyVaultType, null),
            ("storage", StorageType, new() { ["key_vault_id"] = "${resources.vault.id}" }));
        await CreateOrchestrator(score).StartAsync(_application, _deployment, CancellationToken.None);

        await CreateOrchestrator(score).DestroyAsync(_application, _deployment, CancellationToken.None);

        var graph = Assert.Single(_graphs.Items);
        Assert.Equal(2, _resources.Items.Count);
        Assert.All(_resources.Items, r => Assert.Empty(r.Consumers));
        Assert.All(_resources.Items, r => Assert.False(graph.ContainsResource(r.Id)));
    }

    [Fact]
    public async Task DestroyAsync_AfterFailedProvision_MarksInstancesRemoved()
    {
        var score = Score(("storage", StorageType, null));
        _provisioner.Failure = new InvalidOperationException("terraform apply failed");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(score).StartAsync(_application, _deployment, CancellationToken.None));
        _provisioner.Failure = null;

        await CreateOrchestrator(score).DestroyAsync(_application, _deployment, CancellationToken.None);

        Assert.Equal(ResourceInstanceStatus.Removed, Assert.Single(_instances.Items).Status);
    }

    [Fact]
    public async Task DestroyAsync_DeleteFails_KeepsConsumerAndGraphNode()
    {
        var score = Score(("storage", StorageType, null));
        await CreateOrchestrator(score).StartAsync(_application, _deployment, CancellationToken.None);
        _provisioner.Failure = new InvalidOperationException("terraform destroy failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(score).DestroyAsync(_application, _deployment, CancellationToken.None));

        var resource = Assert.Single(_resources.Items);
        Assert.Equal([_application.Id], resource.Consumers);
        Assert.True(Assert.Single(_graphs.Items).ContainsResource(resource.Id));
    }

    [Fact]
    public async Task DestroyAsync_DeleteFails_MarksInstancesRemovalFailed()
    {
        var score = Score(("storage", StorageType, null));
        await CreateOrchestrator(score).StartAsync(_application, _deployment, CancellationToken.None);
        _provisioner.Failure = new InvalidOperationException("terraform destroy failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrchestrator(score).DestroyAsync(_application, _deployment, CancellationToken.None));

        Assert.Equal(ResourceInstanceStatus.RemovalFailed, Assert.Single(_instances.Items).Status);
    }

    [Fact]
    public async Task DestroyAsync_NothingRecorded_DeletesWithoutCreatingRecords()
    {
        await CreateOrchestrator(Score(("storage", StorageType, null)))
            .DestroyAsync(_application, _deployment, CancellationToken.None);

        Assert.Single(_provisioner.Inputs!);
        Assert.Empty(_resources.Items);
        Assert.Empty(_instances.Items);
    }

    private EngineOrchestrator CreateOrchestrator(ScoreFile? scoreFile) =>
        new(NullLogger<EngineOrchestrator>.Instance, new FixedScoreDriver(scoreFile), TemplateRepository.Instance,
            _resources, _instances, _graphs, _provisioner);

    private static ScoreFile Score(params (string Key, string Type, Dictionary<string, string>? Parameters)[] resources) =>
        new()
        {
            ApiVersion = "score.dev/v1b1",
            Metadata = new ScoreMetadata { Name = "orders" },
            Resources = resources.ToDictionary(r => r.Key,
                r => new ScoreResource { Type = r.Type, Parameters = r.Parameters })
        };

    private static Application NewApplication() =>
        Application.Create("orders", new Repository
        {
            Name = "orders",
            Url = new Uri("https://example.com/orders.git"),
            Provider = RepositoryProvider.GitHub
        }, new OrganisationId());

    private static Deployment NewDeployment() =>
        Deployment.Create(new ApplicationId(), new EnvironmentId(Guid.NewGuid()), new CommitId(new string('a', 40)), "test@example.com");

    private sealed class FixedScoreDriver(ScoreFile? scoreFile) : IScoreDriver
    {
        public Task<ScoreFile?> ParseAsync(Deployment deployment, Application application,
            CancellationToken cancellationToken) => Task.FromResult(scoreFile);
    }

    private sealed class RecordingProvisioner : IEngineProvisioner
    {
        public List<ProvisionInput>? Inputs { get; private set; }
        public Exception? Failure { get; set; }

        public Task ProvisionAsync(List<ProvisionInput> inputs, ProvisionContext context,
            CancellationToken cancellationToken = default)
        {
            Inputs = inputs;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task DeleteAsync(List<ProvisionInput> inputs, ProvisionContext context,
            CancellationToken cancellationToken = default)
        {
            Inputs = inputs;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class TemplateRepository : IResourceTemplateRepository
    {
        public static readonly TemplateRepository Instance = new();

        private readonly Dictionary<string, ResourceTemplate> _templates = new[]
        {
            NewTemplate(StorageType, ResourceTemplateVersionState.Active),
            NewTemplate(KeyVaultType, ResourceTemplateVersionState.Active),
            NewTemplate(InactiveType, ResourceTemplateVersionState.Inactive)
        }.ToDictionary(t => t.Type);

        private static ResourceTemplate NewTemplate(string type, ResourceTemplateVersionState state) =>
            ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
            {
                OrganisationId = new OrganisationId(),
                Name = type,
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

        public Task<ResourceTemplate?> GetByTypeAsync(string type, CancellationToken cancellationToken = default) =>
            Task.FromResult(_templates.GetValueOrDefault(type));

        public Task<ResourceTemplate?> CreateAsync(ResourceTemplate environment,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IEnumerable<ResourceTemplate> GetAll() => throw new NotSupportedException();

        public Task<IReadOnlyList<ResourceTemplate>> GetByOrganisationIdsAsync(
            IReadOnlyCollection<OrganisationId> organisationIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResourceTemplate?> GetByIdAsync(ResourceTemplateId id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ResourceTemplate?> UpdateAsync(ResourceTemplate resourceTemplate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(ResourceTemplateId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
