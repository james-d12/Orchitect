using Microsoft.Extensions.Logging.Abstractions;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Configuration.Score;
using Orchitect.Infrastructure.Engine.Configuration.Score.Models;
using Orchitect.Infrastructure.Engine.Provisioner;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Infrastructure.Engine.Unit.Tests;

public sealed class EngineOrchestratorTests
{
    private const string StorageType = "azure-storage-account";

    [Fact]
    public async Task StartAsync_ResourceTemplateMissing_ThrowsWithoutProvisioning()
    {
        var provisioner = new RecordingProvisioner();
        var orchestrator = CreateOrchestrator(Score(("storage", "unknown-type", new() { ["name"] = "x" })),
            provisioner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync(NewApplication(), NewDeployment(), CancellationToken.None));

        Assert.Contains("unknown-type", exception.Message);
        Assert.Contains("storage", exception.Message);
        Assert.Null(provisioner.Inputs);
    }

    [Fact]
    public async Task StartAsync_ParametersMissing_ProvisionsWithEmptyInputs()
    {
        var provisioner = new RecordingProvisioner();
        var orchestrator = CreateOrchestrator(Score(("storage", StorageType, null)), provisioner);

        await orchestrator.StartAsync(NewApplication(), NewDeployment(), CancellationToken.None);

        var input = Assert.Single(provisioner.Inputs!);
        Assert.Empty(input.Inputs);
    }

    [Fact]
    public async Task StartAsync_ScoreFileMissing_Throws()
    {
        var orchestrator = CreateOrchestrator(null, new RecordingProvisioner());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.StartAsync(NewApplication(), NewDeployment(), CancellationToken.None));
    }

    [Fact]
    public async Task DestroyAsync_NoResources_Throws()
    {
        var orchestrator = CreateOrchestrator(Score(), new RecordingProvisioner());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.DestroyAsync(NewApplication(), NewDeployment(), CancellationToken.None));
    }

    private static EngineOrchestrator CreateOrchestrator(ScoreFile? scoreFile, IEngineProvisioner provisioner) =>
        new(NullLogger<EngineOrchestrator>.Instance, new FixedScoreDriver(scoreFile),
            new SingleTemplateRepository(), provisioner);

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
        Deployment.Create(new ApplicationId(), new EnvironmentId(Guid.NewGuid()), new CommitId("abc123"));

    private sealed class FixedScoreDriver(ScoreFile? scoreFile) : IScoreDriver
    {
        public Task<ScoreFile?> ParseAsync(Deployment deployment, Application application,
            CancellationToken cancellationToken) => Task.FromResult(scoreFile);
    }

    private sealed class RecordingProvisioner : IEngineProvisioner
    {
        public List<ProvisionInput>? Inputs { get; private set; }

        public Task ProvisionAsync(List<ProvisionInput> inputs, ProvisionContext context,
            CancellationToken cancellationToken = default)
        {
            Inputs = inputs;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(List<ProvisionInput> inputs, ProvisionContext context,
            CancellationToken cancellationToken = default)
        {
            Inputs = inputs;
            return Task.CompletedTask;
        }
    }

    private sealed class SingleTemplateRepository : IResourceTemplateRepository
    {
        private readonly ResourceTemplate _template = ResourceTemplate.Create(new CreateResourceTemplateRequest
        {
            OrganisationId = new OrganisationId(),
            Name = "Storage Account",
            Type = StorageType,
            Description = "A storage account.",
            Provider = ResourceTemplateProvider.Terraform
        });

        public Task<ResourceTemplate?> GetByTypeAsync(string type, CancellationToken cancellationToken = default) =>
            Task.FromResult(type == StorageType ? _template : null);

        public Task<ResourceTemplate?> CreateAsync(ResourceTemplate environment,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IEnumerable<ResourceTemplate> GetAll() => throw new NotSupportedException();

        public Task<ResourceTemplate?> GetByIdAsync(ResourceTemplateId id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ResourceTemplate?> UpdateAsync(ResourceTemplate resourceTemplate,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(ResourceTemplateId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
