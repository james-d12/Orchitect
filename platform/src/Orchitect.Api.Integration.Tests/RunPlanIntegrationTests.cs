using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AutoFixture;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Api.Endpoints.Engine.Environment;
using Orchitect.Api.Endpoints.Internal;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Api.Shared;
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
using Orchitect.Engine.Dispatch.Auth;
using Orchitect.Engine.Dispatch.Plan;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class RunPlanIntegrationTests : IAsyncLifetime
{
    private readonly Fixture _fixture = new();
    private readonly WebApplicationFactoryWithPostgres _factory;
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];
    private Application _application = null!;
    private Deployment _deployment = null!;

    public RunPlanIntegrationTests(WebApplicationFactoryWithPostgres factory)
    {
        _factory = factory;
    }

    private string KeyVaultType => $"azure-key-vault-{_suffix}";
    private string StorageType => $"azure-storage-account-{_suffix}";
    private string PostgresType => $"azure-postgres-{_suffix}";

    public async Task InitializeAsync()
    {
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var application = await client.CreateApplicationAsync(organisation.Id);
        var environmentResponse = await client.PostAsJsonAsync("/environments",
            new CreateEnvironmentRequest(_fixture.Create<string>(), _fixture.Create<string>(),
                new OrganisationId(organisation.Id)));
        var environment =
            await environmentResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        ArgumentNullException.ThrowIfNull(environment);

        using var scope = _factory.Services.CreateScope();
        _application = await scope.ServiceProvider.GetRequiredService<IApplicationRepository>()
                           .GetByIdAsync(new ApplicationId(application.Id))
                       ?? throw new InvalidOperationException("The application was not created.");
        _deployment = await scope.ServiceProvider.GetRequiredService<IDeploymentRepository>().CreateAsync(
                          Deployment.Create(_application.Id, new EnvironmentId(environment.Id),
                              new CommitId(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20))),
                              "test@example.com"))
                      ?? throw new InvalidOperationException("The deployment was not created.");

        var templates = scope.ServiceProvider.GetRequiredService<IResourceTemplateRepository>();
        foreach (var type in new[] { KeyVaultType, StorageType, PostgresType })
        {
            await templates.CreateAsync(ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
            {
                OrganisationId = new OrganisationId(organisation.Id),
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

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Plan_MultiResourceScore_ReturnsPlanAndRecordsProvisioningResources()
    {
        // Arrange
        var (run, client) = await StartRunAsync(DeploymentRunOperation.Provision);

        // Act
        var response = await client.PostAsJsonAsync(RunnerRoutes.ForRun(run.Id.Value, RunnerRoutes.Plan),
            new ScoreSubmission(MultiResourceScore("Standard_LRS")), RunnerContract.JsonOptions);
        var json = await response.Content.ReadAsStringAsync();
        var plan = await response.Content.ReadFromJsonAsync<RunPlan>(RunnerContract.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"provider\":\"Terraform\"", json);
        Assert.Equal(new RunContext("orders", _deployment.ApplicationId.Value, _deployment.EnvironmentId.Value),
            plan!.Context);
        Assert.Equal(["database", "storage", "vault"], plan.Inputs.Select(i => i.Key).Order());
        var storageInput = plan.Inputs.Single(i => i.Key == "storage");
        Assert.Equal(StorageType, storageInput.TemplateType);
        Assert.Equal(new RunInputSource(new Uri("https://example.com/modules.git"), "v1.0.0", StorageType),
            storageInput.Source);
        Assert.Equal("Standard_LRS", storageInput.Parameters["sku"]);

        var (resources, instances, graph) = await LoadStateAsync();
        Assert.Equal([Slug("database"), Slug("storage"), Slug("vault")], resources.Select(r => r.Slug).Order());
        Assert.All(resources, r => Assert.Equal(_application.OrganisationId, r.OrganisationId));
        Assert.All(resources, r => Assert.Equal([_application.Id], r.Consumers));
        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Provisioning, i.Status));

        Assert.NotNull(graph);
        var vault = resources.Single(r => r.Slug == Slug("vault"));
        var storage = resources.Single(r => r.Slug == Slug("storage"));
        var database = resources.Single(r => r.Slug == Slug("database"));
        Assert.True(graph.HasDependencyPath(storage.Id, vault.Id));
        Assert.True(graph.HasDependencyPath(database.Id, storage.Id));
        Assert.Equal([vault.Id, storage.Id, database.Id], graph.ResolveOrder());
    }

    [Fact]
    public async Task Plan_RepeatCallForTheSameRun_ReturnsStoredPlanWithoutRecordingAgain()
    {
        // Arrange
        var (run, client) = await StartRunAsync(DeploymentRunOperation.Provision);
        var route = RunnerRoutes.ForRun(run.Id.Value, RunnerRoutes.Plan);
        var first = await client.PostAsJsonAsync(route, new ScoreSubmission(MultiResourceScore("Standard_LRS")),
            RunnerContract.JsonOptions);
        var firstPlan = await first.Content.ReadFromJsonAsync<RunPlan>(RunnerContract.JsonOptions);
        var (_, instancesBefore, _) = await LoadStateAsync();

        // Act
        var second = await client.PostAsJsonAsync(route, new ScoreSubmission(MultiResourceScore("Standard_GRS")),
            RunnerContract.JsonOptions);
        var secondPlan = await second.Content.ReadFromJsonAsync<RunPlan>(RunnerContract.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstPlan!.Context, secondPlan!.Context);
        Assert.Equal("Standard_LRS", secondPlan.Inputs.Single(i => i.Key == "storage").Parameters["sku"]);
        var (_, instancesAfter, _) = await LoadStateAsync();
        Assert.Equal(instancesBefore.ToDictionary(i => i.Id, i => i.UpdatedAt),
            instancesAfter.ToDictionary(i => i.Id, i => i.UpdatedAt));
    }

    [Fact]
    public async Task Plan_UnknownResourceType_Returns400WithoutRecording()
    {
        // Arrange
        var (run, client) = await StartRunAsync(DeploymentRunOperation.Provision);
        var score = MultiResourceScore("Standard_LRS");
        score.Resources!["vault"] = score.Resources["vault"] with { Type = $"unknown-{_suffix}" };

        // Act
        var response = await client.PostAsJsonAsync(RunnerRoutes.ForRun(run.Id.Value, RunnerRoutes.Plan),
            new ScoreSubmission(score), RunnerContract.JsonOptions);
        var body = await response.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = Assert.Single(body!.Errors);
        Assert.Equal(PlanRunEndpoint.InvalidErrorCode, error.Code);
        Assert.Contains($"unknown-{_suffix}", error.Message);
        var (resources, _, _) = await LoadStateAsync();
        Assert.Empty(resources);
    }

    [Fact]
    public async Task Plan_SubmissionWithoutScoreFile_Returns400()
    {
        // Arrange
        var (run, client) = await StartRunAsync(DeploymentRunOperation.Provision);

        // Act
        var response = await client.PostAsync(RunnerRoutes.ForRun(run.Id.Value, RunnerRoutes.Plan),
            JsonContent.Create(new { scoreFile = (object?)null }));
        var body = await response.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PlanRunEndpoint.InvalidErrorCode, Assert.Single(body!.Errors).Code);
    }

    [Fact]
    public async Task Finish_ProvisionSucceeds_PersistsActiveInstancesWithOutput()
    {
        await RunAsync(DeploymentRunOperation.Provision, MultiResourceScore("Standard_LRS"), RunOutcome.Succeeded);

        var (_, instances, _) = await LoadStateAsync();
        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Active, i.Status));
        Assert.All(instances, i => Assert.Equal("orders", i.Output!.Workspace));
        Assert.All(instances, i => Assert.Equal(new Uri("https://example.com/modules.git"), i.Output!.Location));
    }

    [Fact]
    public async Task Finish_ProvisionFails_PersistsFailedInstances()
    {
        await RunAsync(DeploymentRunOperation.Provision, MultiResourceScore("Standard_LRS"), RunOutcome.Failed);

        var (resources, instances, _) = await LoadStateAsync();
        Assert.Equal(3, resources.Count);
        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Failed, i.Status));
    }

    [Fact]
    public async Task Plan_Redeploy_UpdatesExistingRecords()
    {
        await RunAsync(DeploymentRunOperation.Provision, MultiResourceScore("Standard_LRS"), RunOutcome.Succeeded);

        await RunAsync(DeploymentRunOperation.Provision, MultiResourceScore("Standard_GRS"), RunOutcome.Succeeded);

        var (resources, instances, _) = await LoadStateAsync();
        Assert.Equal(3, resources.Count);
        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Active, i.Status));
        var storage = resources.Single(r => r.Slug == Slug("storage"));
        Assert.Equal("Standard_GRS",
            instances.Single(i => i.ResourceId == storage.Id).InputParameters["sku"].GetString());
    }

    [Fact]
    public async Task Plan_ReferenceRemovedFromScore_PersistsRemovedDependency()
    {
        await RunAsync(DeploymentRunOperation.Provision, MultiResourceScore("Standard_LRS"), RunOutcome.Succeeded);
        var score = MultiResourceScore("Standard_LRS");
        score.Resources!["storage"].Parameters!.Remove("key_vault_id");

        await RunAsync(DeploymentRunOperation.Provision, score, RunOutcome.Succeeded);

        var (resources, _, graph) = await LoadStateAsync();
        var storage = resources.Single(r => r.Slug == Slug("storage"));
        var vault = resources.Single(r => r.Slug == Slug("vault"));
        Assert.NotNull(graph);
        Assert.False(graph.HasDependencyPath(storage.Id, vault.Id));
        Assert.Equal(1, graph.DependencyCount(resources.Single(r => r.Slug == Slug("database")).Id));
    }

    [Fact]
    public async Task Plan_Destroy_PersistsRemovingInstances()
    {
        await RunAsync(DeploymentRunOperation.Provision, MultiResourceScore("Standard_LRS"), RunOutcome.Succeeded);
        var (run, client) = await StartRunAsync(DeploymentRunOperation.Destroy);

        var response = await client.PostAsJsonAsync(RunnerRoutes.ForRun(run.Id.Value, RunnerRoutes.Plan),
            new ScoreSubmission(MultiResourceScore("Standard_LRS")), RunnerContract.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (_, instances, _) = await LoadStateAsync();
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Removing, i.Status));
    }

    [Fact]
    public async Task Finish_DestroyAfterFailedDeploy_PersistsRemovedInstances()
    {
        await RunAsync(DeploymentRunOperation.Provision, MultiResourceScore("Standard_LRS"), RunOutcome.Failed);

        await RunAsync(DeploymentRunOperation.Destroy, MultiResourceScore("Standard_LRS"), RunOutcome.Succeeded);

        var (_, instances, _) = await LoadStateAsync();
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Removed, i.Status));
    }

    [Fact]
    public async Task Finish_DestroyAfterDeploy_PersistsRemovedInstancesAndReleasesResources()
    {
        await RunAsync(DeploymentRunOperation.Provision, MultiResourceScore("Standard_LRS"), RunOutcome.Succeeded);

        await RunAsync(DeploymentRunOperation.Destroy, MultiResourceScore("Standard_LRS"), RunOutcome.Succeeded);

        var (resources, instances, graph) = await LoadStateAsync();
        Assert.Equal(3, instances.Count);
        Assert.All(instances, i => Assert.Equal(ResourceInstanceStatus.Removed, i.Status));
        Assert.Equal(3, resources.Count);
        Assert.All(resources, r => Assert.Empty(r.Consumers));
        Assert.NotNull(graph);
        Assert.All(resources, r => Assert.False(graph.ContainsResource(r.Id)));
    }

    private async Task RunAsync(DeploymentRunOperation operation, ScoreFile scoreFile, RunOutcome outcome)
    {
        var (run, client) = await StartRunAsync(operation);
        var response = await client.PostAsJsonAsync(RunnerRoutes.ForRun(run.Id.Value, RunnerRoutes.Plan),
            new ScoreSubmission(scoreFile), RunnerContract.JsonOptions);
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRunPlanner>()
            .FinishAsync(run.Id, outcome, CancellationToken.None);
    }

    private async Task<(DeploymentRun Run, HttpClient Client)> StartRunAsync(DeploymentRunOperation operation)
    {
        var token = RunnerToken.Generate();

        using var scope = _factory.Services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>().CreateAsync(
            DeploymentRun.Queue(_deployment.Id, operation).Start()
                .IssueToken(token.Hash, DateTime.UtcNow.AddHours(1)));
        ArgumentNullException.ThrowIfNull(run);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        client.DefaultRequestHeaders.Add(RunnerContract.HeaderName, RunnerContract.Version.ToString());

        return (run, client);
    }

    private async Task<(IReadOnlyList<Resource> Resources, IReadOnlyList<ResourceInstance> Instances,
        ResourceDependencyGraph? Graph)> LoadStateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var resources = await scope.ServiceProvider.GetRequiredService<IResourceRepository>()
            .GetByEnvironmentAsync(_deployment.EnvironmentId);
        var instances = await scope.ServiceProvider.GetRequiredService<IResourceInstanceRepository>()
            .GetByEnvironmentAsync(_deployment.EnvironmentId);
        var graph = await scope.ServiceProvider.GetRequiredService<IResourceDependencyGraphRepository>()
            .GetByEnvironmentAsync(_deployment.EnvironmentId);
        return (resources, instances, graph);
    }

    private string Slug(string key) => Resource.CreateSlug($"{_application.Name}-{key}");

    private ScoreFile MultiResourceScore(string storageSku) => new()
    {
        ApiVersion = "score.dev/v1b1",
        Metadata = new ScoreMetadata { Name = "orders" },
        Resources = new Dictionary<string, ScoreResource>
        {
            ["vault"] = new() { Type = KeyVaultType },
            ["storage"] = new()
            {
                Type = StorageType,
                Parameters = new() { ["sku"] = storageSku, ["key_vault_id"] = "${resources.vault.id}" }
            },
            ["database"] = new()
            {
                Type = PostgresType,
                Parameters = new() { ["backup_container"] = "${resources.storage.container}" }
            }
        }
    };
}
