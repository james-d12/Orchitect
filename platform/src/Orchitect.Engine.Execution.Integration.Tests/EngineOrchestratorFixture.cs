using Microsoft.Extensions.DependencyInjection;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Engine.Execution.Configuration.Score;
using Orchitect.Engine.Execution.Provisioner;
using Orchitect.Persistence;
using Testcontainers.PostgreSql;

namespace Orchitect.Engine.Execution.Integration.Tests;

public sealed class EngineOrchestratorFixture : IAsyncLifetime
{
    public const string KeyVaultType = "azure-key-vault";
    public const string StorageType = "azure-storage-account";
    public const string PostgresType = "azure-postgres";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:15.1").Build();
    private ServiceProvider? _services;

    public StubScoreDriver ScoreDriver { get; } = new();
    public StubProvisioner Provisioner { get; } = new();
    public Organisation Organisation { get; } = Organisation.Create("engine-orchestrator-tests");

    public IServiceScope CreateScope() => _services!.CreateScope();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__orchitect", _postgres.GetConnectionString());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistenceServices();
        services.AddSingleton<IScoreDriver>(ScoreDriver);
        services.AddSingleton<IEngineProvisioner>(Provisioner);
        services.AddScoped<IEngineOrchestrator, EngineOrchestrator>();
        await services.ApplyMigrations();
        _services = services.BuildServiceProvider();

        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrchitectDbContext>();
        dbContext.Organisations.Add(Organisation);
        await dbContext.SaveChangesAsync();

        var templates = scope.ServiceProvider.GetRequiredService<IResourceTemplateRepository>();
        foreach (var type in new[] { KeyVaultType, StorageType, PostgresType })
        {
            await templates.CreateAsync(ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
            {
                OrganisationId = Organisation.Id,
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
                State = ResourceTemplateVersionState.Active
            }));
        }
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__orchitect", null);
        await _postgres.DisposeAsync();
    }

    public sealed class StubScoreDriver : IScoreDriver
    {
        public ScoreFile? ScoreFile { get; set; }

        public Task<ScoreFile?> ParseAsync(Deployment deployment, Application application,
            CancellationToken cancellationToken) => Task.FromResult(ScoreFile);
    }

    public sealed class StubProvisioner : IEngineProvisioner
    {
        public Exception? Failure { get; set; }

        public Task ProvisionAsync(List<ProvisionInput> inputs, ProvisionContext context,
            CancellationToken cancellationToken = default) =>
            Failure is null ? Task.CompletedTask : Task.FromException(Failure);

        public Task DeleteAsync(List<ProvisionInput> inputs, ProvisionContext context,
            CancellationToken cancellationToken = default) =>
            Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}
