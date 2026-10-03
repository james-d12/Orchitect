using Microsoft.Extensions.DependencyInjection;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Engine.Contracts.Score;
using Orchitect.Persistence;
using Environment = Orchitect.Domain.Engine.Environment.Environment;

namespace Orchitect.Engine.Execution.Integration.Tests;

public sealed class EngineOrchestratorIntegrationTests(EngineOrchestratorFixture fixture)
    : IClassFixture<EngineOrchestratorFixture>, IAsyncLifetime
{
    private readonly Organisation _organisation = Organisation.Create($"orders-{Guid.NewGuid():N}");
    private Application _application = null!;
    private Environment _environment = null!;

    private Deployment _deployment = null!;
    private DeploymentRun _run = null!;

    public async Task InitializeAsync()
    {
        _application = Application.Create("orders", new Repository
        {
            Name = "orders",
            Url = new Uri("https://example.com/orders.git"),
            Provider = RepositoryProvider.GitHub
        }, _organisation.Id);
        _environment = Environment.Create("orders", "Orchestrator tests.", _organisation.Id);
        _deployment = Deployment.Create(_application.Id, _environment.Id, new CommitId(new string('a', 40)),
            "test@example.com").Start();
        _run = DeploymentRun.Queue(_deployment.Id, DeploymentRunOperation.Provision).Start();

        using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrchitectDbContext>();
        dbContext.Organisations.Add(_organisation);
        dbContext.Applications.Add(_application);
        dbContext.Environments.Add(_environment);
        dbContext.Deployments.Add(_deployment);
        dbContext.DeploymentRuns.Add(_run);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task StartAsync_MultiResourceScoreFile_RecordsResourcesInstancesAndGraph()
    {
        fixture.ScoreDriver.ScoreFile = MultiResourceScore("Standard_LRS");

        await StartAsync();

        var (resources, instances, graph) = await LoadStateAsync();
        Assert.Equal(["orders-database", "orders-storage", "orders-vault"],
            resources.Select(r => r.Slug).Order());
        Assert.All(resources, r => Assert.Equal(_organisation.Id, r.OrganisationId));

        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Provisioning, i.Status));
        var run = await LoadRunAsync();
        Assert.Equal("orders", run.ProjectName);
        Assert.Equal(instances.Select(i => i.Id).OrderBy(id => id.Value), run.InstanceIds.OrderBy(id => id.Value));
        var storage = resources.Single(r => r.Slug == "orders-storage");
        Assert.Equal("Standard_LRS",
            instances.Single(i => i.ResourceId == storage.Id).InputParameters["sku"].GetString());

        Assert.NotNull(graph);
        var vault = resources.Single(r => r.Slug == "orders-vault");
        var database = resources.Single(r => r.Slug == "orders-database");
        Assert.All(resources, r => Assert.True(graph.ContainsResource(r.Id)));
        Assert.True(graph.HasDependencyPath(storage.Id, vault.Id));
        Assert.True(graph.HasDependencyPath(database.Id, storage.Id));
        Assert.Equal([vault.Id, storage.Id, database.Id], graph.ResolveOrder());
        Assert.All(resources, r => Assert.Equal([_application.Id], r.Consumers));
    }

    [Fact]
    public async Task StartAsync_ProvisionFails_LeavesPlannedInstancesForTheRunToSettle()
    {
        fixture.ScoreDriver.ScoreFile = MultiResourceScore("Standard_LRS");
        fixture.Provisioner.Failure = new InvalidOperationException("terraform apply failed");

        await Assert.ThrowsAsync<InvalidOperationException>(StartAsync);

        var (resources, instances, _) = await LoadStateAsync();
        Assert.Equal(3, resources.Count);
        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Provisioning, i.Status));
        Assert.Equal(3, (await LoadRunAsync()).InstanceIds.Count);
    }

    [Fact]
    public async Task StartAsync_Redeploy_UpdatesExistingRecords()
    {
        fixture.ScoreDriver.ScoreFile = MultiResourceScore("Standard_LRS");
        await StartAsync();

        fixture.ScoreDriver.ScoreFile = MultiResourceScore("Standard_GRS");
        await StartAsync();

        var (resources, instances, _) = await LoadStateAsync();
        Assert.Equal(3, resources.Count);
        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Provisioning, i.Status));
        var storage = resources.Single(r => r.Slug == "orders-storage");
        Assert.Equal("Standard_GRS",
            instances.Single(i => i.ResourceId == storage.Id).InputParameters["sku"].GetString());
    }

    [Fact]
    public async Task StartAsync_ReferenceRemovedFromScore_PersistsRemovedDependency()
    {
        fixture.ScoreDriver.ScoreFile = MultiResourceScore("Standard_LRS");
        await StartAsync();

        fixture.ScoreDriver.ScoreFile = MultiResourceScore("Standard_LRS");
        fixture.ScoreDriver.ScoreFile.Resources!["storage"].Parameters!.Remove("key_vault_id");
        await StartAsync();

        var (resources, _, graph) = await LoadStateAsync();
        var storage = resources.Single(r => r.Slug == "orders-storage");
        var vault = resources.Single(r => r.Slug == "orders-vault");
        Assert.NotNull(graph);
        Assert.False(graph.HasDependencyPath(storage.Id, vault.Id));
        Assert.Equal(1, graph.DependencyCount(resources.Single(r => r.Slug == "orders-database").Id));
    }

    [Fact]
    public async Task DestroyAsync_AfterFailedDeploy_PersistsRemovingInstances()
    {
        fixture.ScoreDriver.ScoreFile = MultiResourceScore("Standard_LRS");
        fixture.Provisioner.Failure = new InvalidOperationException("terraform apply failed");
        await Assert.ThrowsAsync<InvalidOperationException>(StartAsync);
        fixture.Provisioner.Failure = null;

        await DestroyAsync();

        var (_, instances, _) = await LoadStateAsync();
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Removing, i.Status));
    }

    [Fact]
    public async Task DestroyAsync_AfterDeploy_RecordsRemovingInstancesAndKeepsResourcesForTheRun()
    {
        fixture.ScoreDriver.ScoreFile = MultiResourceScore("Standard_LRS");
        await StartAsync();

        await DestroyAsync();

        var (resources, instances, graph) = await LoadStateAsync();
        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Removing, i.Status));
        Assert.Equal(3, (await LoadRunAsync()).InstanceIds.Count);
        Assert.Equal(3, resources.Count);
        Assert.All(resources, r => Assert.Equal([_application.Id], r.Consumers));
        Assert.NotNull(graph);
        Assert.All(resources, r => Assert.True(graph.ContainsResource(r.Id)));
    }

    public Task DisposeAsync()
    {
        fixture.ScoreDriver.ScoreFile = null;
        fixture.Provisioner.Failure = null;
        return Task.CompletedTask;
    }

    private async Task StartAsync()
    {
        using var scope = fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IEngineOrchestrator>()
            .StartAsync(_application, _deployment, _run.Id, CancellationToken.None);
    }

    private async Task DestroyAsync()
    {
        using var scope = fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IEngineOrchestrator>()
            .DestroyAsync(_application, _deployment, _run.Id, CancellationToken.None);
    }

    private async Task<DeploymentRun> LoadRunAsync()
    {
        using var scope = fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>().GetByIdAsync(_run.Id)
               ?? throw new InvalidOperationException("The run was not found.");
    }

    private async Task<(IReadOnlyList<Resource> Resources, IReadOnlyList<ResourceInstance> Instances,
        ResourceDependencyGraph? Graph)> LoadStateAsync()
    {
        using var scope = fixture.CreateScope();
        var resources = await scope.ServiceProvider.GetRequiredService<IResourceRepository>()
            .GetByEnvironmentAsync(_deployment.EnvironmentId);
        var instances = await scope.ServiceProvider.GetRequiredService<IResourceInstanceRepository>()
            .GetByEnvironmentAsync(_deployment.EnvironmentId);
        var graph = await scope.ServiceProvider.GetRequiredService<IResourceDependencyGraphRepository>()
            .GetByEnvironmentAsync(_deployment.EnvironmentId);
        return (resources, instances, graph);
    }

    private static ScoreFile MultiResourceScore(string storageSku) => new()
    {
        ApiVersion = "score.dev/v1b1",
        Metadata = new ScoreMetadata { Name = "orders" },
        Resources = new Dictionary<string, ScoreResource>
        {
            ["vault"] = new() { Type = EngineOrchestratorFixture.KeyVaultType },
            ["storage"] = new()
            {
                Type = EngineOrchestratorFixture.StorageType,
                Parameters = new() { ["sku"] = storageSku, ["key_vault_id"] = "${resources.vault.id}" }
            },
            ["database"] = new()
            {
                Type = EngineOrchestratorFixture.PostgresType,
                Parameters = new() { ["backup_container"] = "${resources.storage.container}" }
            }
        }
    };
}
