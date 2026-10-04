using Microsoft.Extensions.Logging;
using NSubstitute;
using Orchitect.Domain.Core;
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
using Orchitect.Engine.Dispatch.Completion;
using Orchitect.Engine.Dispatch.Plan;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Plan;

internal sealed class RunTestContext
{
    public const string StorageType = "azure-storage-account";
    public const string KeyVaultType = "azure-key-vault";
    public const string InactiveType = "inactive-template";

    private readonly Dictionary<DeploymentRunId, DeploymentRun> _runs = [];
    private readonly IDeploymentRunRepository _runRepository = Substitute.For<IDeploymentRunRepository>();
    private readonly IDeploymentRepository _deploymentRepository = Substitute.For<IDeploymentRepository>();
    private readonly IApplicationRepository _applicationRepository = Substitute.For<IApplicationRepository>();
    private readonly IResourceTemplateRepository _templateRepository = Substitute.For<IResourceTemplateRepository>();
    private readonly Dictionary<string, ResourceTemplate> _templates;

    public RunTestContext()
    {
        Application = Application.Create("orders", new Repository
        {
            Name = "orders",
            Url = new Uri("https://example.com/orders.git"),
            Provider = RepositoryProvider.GitHub
        }, new OrganisationId());
        Deployment = Deployment.Create(Application.Id, new EnvironmentId(Guid.NewGuid()),
            new CommitId(new string('a', 40)), "test@example.com");

        _runRepository.GetByIdAsync(Arg.Any<DeploymentRunId>(), Arg.Any<CancellationToken>())
            .Returns(call => _runs.GetValueOrDefault(call.Arg<DeploymentRunId>()));
        _deploymentRepository.GetByIdAsync(Deployment.Id, Arg.Any<CancellationToken>()).Returns(Deployment);
        _applicationRepository.GetByIdAsync(Application.Id, Arg.Any<CancellationToken>()).Returns(Application);

        _templates = new[]
        {
            NewTemplate(StorageType, ResourceTemplateVersionState.Active),
            NewTemplate(KeyVaultType, ResourceTemplateVersionState.Active),
            NewTemplate(InactiveType, ResourceTemplateVersionState.Inactive)
        }.ToDictionary(t => t.Type);
        _templateRepository.GetByTypeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => _templates.GetValueOrDefault(call.Arg<string>()));
    }

    public Application Application { get; }
    public Deployment Deployment { get; }
    public InMemoryPlanRepository Plans { get; } = new();
    public InMemoryResourceRepository Resources { get; } = new();
    public InMemoryResourceInstanceRepository Instances { get; } = new();
    public InMemoryResourceDependencyGraphRepository Graphs { get; } = new();
    public RecordingLogger<RunPlanner> PlannerLogger { get; } = new();

    public ResourceTemplate Template(string type) => _templates[type];

    public RunPlanner CreatePlanner() =>
        new(PlannerLogger, new PassThroughUnitOfWork(), RunRepositories(), _templateRepository,
            ResourceRepositories());

    public RunCompleter CreateCompleter() =>
        new(new RecordingLogger<RunCompleter>(), new PassThroughUnitOfWork(), RunRepositories(),
            ResourceRepositories());

    private RunRepositories RunRepositories() =>
        new(_runRepository, Plans, _deploymentRepository, _applicationRepository);

    private ResourceRepositories ResourceRepositories() => new(Resources, Instances, Graphs);

    public DeploymentRun AddRun(DeploymentRun run)
    {
        _runs[run.Id] = run;
        return run;
    }

    public async Task<(DeploymentRunId RunId, RunPlan Plan)> PlanAsync(DeploymentRunOperation operation,
        ScoreFile scoreFile)
    {
        var run = AddRun(DeploymentRun.Queue(Deployment.Id, operation).Start());
        return (run.Id, await CreatePlanner().PlanAsync(run.Id, scoreFile, CancellationToken.None));
    }

    public async Task<DeploymentRunId> RunAsync(DeploymentRunOperation operation, ScoreFile scoreFile,
        RunOutcome outcome)
    {
        var (runId, _) = await PlanAsync(operation, scoreFile);
        await CreateCompleter().CompleteAsync(runId, outcome, CancellationToken.None);
        return runId;
    }

    public static ScoreFile Score(
        params (string Key, string Type, Dictionary<string, string>? Parameters)[] resources) =>
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

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
            CancellationToken cancellationToken = default) => work(cancellationToken);

        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            work(cancellationToken);
    }

    internal sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    internal sealed class InMemoryPlanRepository : IDeploymentRunPlanRepository
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

    internal sealed class InMemoryResourceRepository : IResourceRepository
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

    internal sealed class InMemoryResourceInstanceRepository : IResourceInstanceRepository
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

    internal sealed class InMemoryResourceDependencyGraphRepository : IResourceDependencyGraphRepository
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
