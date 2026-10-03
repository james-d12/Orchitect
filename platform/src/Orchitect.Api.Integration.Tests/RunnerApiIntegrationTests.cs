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
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Auth;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class RunnerApiIntegrationTests
{
    private readonly Fixture _fixture = new();
    private readonly WebApplicationFactoryWithPostgres _factory;

    public RunnerApiIntegrationTests(WebApplicationFactoryWithPostgres factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(DeploymentRunOperation.Provision, RunnerOperation.Provision)]
    [InlineData(DeploymentRunOperation.Destroy, RunnerOperation.Destroy)]
    public async Task RunnerApi_WhenGettingRunDescriptor_ShouldReturn200OkWithDescriptor(
        DeploymentRunOperation operation, RunnerOperation expectedOperation)
    {
        // Arrange
        var (deployment, run, token) = await SeedRunAsync(operation);
        var client = CreateRunnerClient(token, RunnerContract.Version.ToString());

        // Act
        var response = await client.GetAsync(RunnerRoutes.ForRun(run.Id.Value));
        var json = await response.Content.ReadAsStringAsync();
        var body = await response.Content.ReadFromJsonAsync<RunDescriptor>(RunnerContract.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"\"operation\":\"{expectedOperation}\"", json);
        Assert.Equal(new RunDescriptor(
            run.Id.Value,
            expectedOperation,
            new Uri("https://github.com/test/repo"),
            deployment.CommitId.Value,
            deployment.ApplicationId.Value,
            deployment.EnvironmentId.Value), body);
    }

    [Fact]
    public async Task RunnerApi_WhenContractHeaderIsMissing_ShouldReturn400BadRequest()
    {
        // Arrange
        var (_, run, token) = await SeedRunAsync(DeploymentRunOperation.Provision);
        var client = CreateRunnerClient(token, null);

        // Act
        var response = await client.GetAsync(RunnerRoutes.ForRun(run.Id.Value));
        var body = await response.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = Assert.Single(body!.Errors);
        Assert.Equal(RunnerContractFilter.MismatchErrorCode, error.Code);
        Assert.Contains("was none", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("not-a-version")]
    public async Task RunnerApi_WhenContractHeaderIsMismatched_ShouldReturn400BadRequest(string version)
    {
        // Arrange
        var (_, run, token) = await SeedRunAsync(DeploymentRunOperation.Provision);
        var client = CreateRunnerClient(token, version);

        // Act
        var response = await client.GetAsync(RunnerRoutes.ForRun(run.Id.Value));
        var body = await response.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = Assert.Single(body!.Errors);
        Assert.Equal(RunnerContractFilter.MismatchErrorCode, error.Code);
        Assert.Contains($"'{version}'", error.Message);
    }

    [Fact]
    public async Task RunnerApi_WhenContractHeaderIsMismatchedWithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (_, run, _) = await SeedRunAsync(DeploymentRunOperation.Provision);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(RunnerContract.HeaderName, "999");

        // Act
        var response = await client.GetAsync(RunnerRoutes.ForRun(run.Id.Value));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunnerApi_WhenUserJwtIsUsed_ShouldReturn401Unauthorized()
    {
        // Arrange
        var (_, run, _) = await SeedRunAsync(DeploymentRunOperation.Provision);
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        client.DefaultRequestHeaders.Add(RunnerContract.HeaderName, RunnerContract.Version.ToString());

        // Act
        var response = await client.GetAsync(RunnerRoutes.ForRun(run.Id.Value));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunnerApi_WhenProvisionSucceeds_ShouldActivateInstancesAndDeploy()
    {
        // Arrange
        var seeded = await SeedPlannedRunAsync(DeploymentRunOperation.Provision);
        var client = CreateRunnerClient(seeded.Token, RunnerContract.Version.ToString());

        // Act
        var response = await client.PostAsJsonAsync(RunnerRoutes.ForRun(seeded.Run.Id.Value, RunnerRoutes.Complete),
            new RunCompletion(RunOutcome.Succeeded, null), RunnerContract.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var (deployment, run, instance, resource, graph) = await LoadAsync(seeded);
        Assert.Equal(DeploymentStatus.Deployed, deployment.Status);
        Assert.Equal(DeploymentRunStatus.Succeeded, run.Status);
        Assert.Null(run.TokenHash);
        Assert.Null(run.ExitCode);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
        Assert.Equal("orders", instance.Output!.Workspace);
        Assert.Equal(new Uri("https://example.com/modules.git"), instance.Output.Location);
        Assert.Equal([deployment.ApplicationId], resource.Consumers);
        Assert.True(graph.ContainsResource(resource.Id));
    }

    [Fact]
    public async Task RunnerApi_WhenProvisionFails_ShouldFailInstancesAndDeploymentWithTheSummary()
    {
        // Arrange
        var seeded = await SeedPlannedRunAsync(DeploymentRunOperation.Provision);
        var client = CreateRunnerClient(seeded.Token, RunnerContract.Version.ToString());

        // Act
        var response = await client.PostAsJsonAsync(RunnerRoutes.ForRun(seeded.Run.Id.Value, RunnerRoutes.Complete),
            new RunCompletion(RunOutcome.Failed, "terraform apply failed"), RunnerContract.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var (deployment, run, instance, _, _) = await LoadAsync(seeded);
        Assert.Equal(DeploymentStatus.Failed, deployment.Status);
        Assert.Equal("terraform apply failed", deployment.ErrorSummary);
        Assert.Equal(DeploymentRunStatus.Failed, run.Status);
        Assert.Equal("terraform apply failed", run.ErrorSummary);
        Assert.Equal(ResourceInstanceStatus.Failed, instance.Status);
    }

    [Fact]
    public async Task RunnerApi_WhenDestroySucceeds_ShouldRemoveInstancesAndReleaseResources()
    {
        // Arrange
        var seeded = await SeedPlannedRunAsync(DeploymentRunOperation.Destroy);
        var client = CreateRunnerClient(seeded.Token, RunnerContract.Version.ToString());

        // Act
        var response = await client.PostAsJsonAsync(RunnerRoutes.ForRun(seeded.Run.Id.Value, RunnerRoutes.Complete),
            new RunCompletion(RunOutcome.Succeeded, null), RunnerContract.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var (deployment, _, instance, resource, graph) = await LoadAsync(seeded);
        Assert.Equal(DeploymentStatus.Destroyed, deployment.Status);
        Assert.Equal(ResourceInstanceStatus.Removed, instance.Status);
        Assert.Empty(resource.Consumers);
        Assert.False(graph.ContainsResource(resource.Id));
    }

    [Fact]
    public async Task RunnerApi_WhenReportIsRepeated_ShouldRejectTheRevokedTokenAndKeepTheFirstOutcome()
    {
        // Arrange
        var seeded = await SeedPlannedRunAsync(DeploymentRunOperation.Provision);
        var client = CreateRunnerClient(seeded.Token, RunnerContract.Version.ToString());
        var route = RunnerRoutes.ForRun(seeded.Run.Id.Value, RunnerRoutes.Complete);
        await client.PostAsJsonAsync(route, new RunCompletion(RunOutcome.Succeeded, null), RunnerContract.JsonOptions);

        // Act
        var response = await client.PostAsJsonAsync(route, new RunCompletion(RunOutcome.Failed, "late"),
            RunnerContract.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var (deployment, run, instance, _, _) = await LoadAsync(seeded);
        Assert.Equal(DeploymentStatus.Deployed, deployment.Status);
        Assert.Equal(DeploymentRunStatus.Succeeded, run.Status);
        Assert.Equal(ResourceInstanceStatus.Active, instance.Status);
    }

    [Fact]
    public async Task RunnerApi_WhenOutcomeIsUnknown_ShouldReturn400BadRequest()
    {
        // Arrange
        var seeded = await SeedPlannedRunAsync(DeploymentRunOperation.Provision);
        var client = CreateRunnerClient(seeded.Token, RunnerContract.Version.ToString());

        // Act
        var response = await client.PostAsync(RunnerRoutes.ForRun(seeded.Run.Id.Value, RunnerRoutes.Complete),
            new StringContent("""{"outcome":7}""", System.Text.Encoding.UTF8, "application/json"));
        var body = await response.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CompleteRunEndpoint.InvalidOutcomeErrorCode, Assert.Single(body!.Errors).Code);
        var (_, run, _, _, _) = await LoadAsync(seeded);
        Assert.Equal(DeploymentRunStatus.Running, run.Status);
    }

    [Fact]
    public async Task DeploymentRunRepository_WhenFinishingTwice_ShouldOnlySaveTheFirst()
    {
        // Arrange
        var seeded = await SeedPlannedRunAsync(DeploymentRunOperation.Provision);
        using var scope = _factory.Services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>();

        // Act
        var first = await runs.TryFinishAsync(seeded.Run.Succeed(0, "container-1"));
        var second = await runs.TryFinishAsync(seeded.Run.Fail("late", 1));

        // Assert
        Assert.True(first);
        Assert.False(second);
        var stored = await runs.GetByIdAsync(seeded.Run.Id);
        Assert.Equal(DeploymentRunStatus.Succeeded, stored!.Status);
        Assert.Equal(0, stored.ExitCode);
        Assert.Equal("container-1", stored.RunnerId);
        Assert.Null(stored.TokenHash);
        Assert.Equal("orders", stored.ProjectName);
        Assert.Equal([seeded.InstanceId], stored.InstanceIds);
    }

    private HttpClient CreateRunnerClient(RunnerToken token, string? contractVersion)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        if (contractVersion is not null)
        {
            client.DefaultRequestHeaders.Add(RunnerContract.HeaderName, contractVersion);
        }

        return client;
    }

    private async Task<(Deployment, DeploymentRun, RunnerToken)> SeedRunAsync(DeploymentRunOperation operation)
    {
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);
        var token = RunnerToken.Generate();

        using var scope = _factory.Services.CreateScope();
        var deployment = await scope.ServiceProvider.GetRequiredService<IDeploymentRepository>().CreateAsync(
            Deployment.Create(applicationId, environmentId,
                new CommitId(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20))), "test@example.com"));
        ArgumentNullException.ThrowIfNull(deployment);

        var run = await scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>().CreateAsync(
            DeploymentRun.Queue(deployment.Id, operation).Start()
                .IssueToken(token.Hash, DateTime.UtcNow.AddHours(1)));
        ArgumentNullException.ThrowIfNull(run);

        return (deployment, run, token);
    }

    private async Task<PlannedRun> SeedPlannedRunAsync(DeploymentRunOperation operation)
    {
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var organisationId = new OrganisationId(organisation.Id);
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client, organisation.Id);
        var token = RunnerToken.Generate();

        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var template = ResourceTemplate.CreateWithVersion(new CreateResourceTemplateWithVersionRequest
        {
            OrganisationId = organisationId,
            Name = "storage",
            Type = $"storage-{Guid.NewGuid():N}",
            Description = "A storage account.",
            Provider = ResourceTemplateProvider.Terraform,
            Version = "1.0.0",
            Source = new ResourceTemplateVersionSource
            {
                BaseUrl = new Uri("https://example.com/modules.git"),
                FolderPath = "storage",
                Tag = "v1.0.0"
            },
            Notes = "Initial version.",
            State = ResourceTemplateVersionState.Active
        });
        await services.GetRequiredService<IResourceTemplateRepository>().CreateAsync(template);

        var resource = Resource.Create(new CreateResourceRequest(organisationId, $"orders-{Guid.NewGuid():N}",
            string.Empty, template.Id, environmentId, ResourceKind.Direct, applicationId));
        resource.AddConsumer(applicationId);
        await services.GetRequiredService<IResourceRepository>().CreateAsync(resource);

        var graph = ResourceDependencyGraph.Create(organisationId, environmentId);
        graph.AddResource(resource.Id);
        await services.GetRequiredService<IResourceDependencyGraphRepository>().CreateAsync(graph);

        var instance = ResourceInstance.Create(new CreateResourceInstanceRequest(resource.Id, organisationId,
            "orders-instance", template.GetLatestVersion()!.Id, environmentId));

        if (operation == DeploymentRunOperation.Destroy)
        {
            instance.Transition(ResourceInstanceStatus.PendingRemoval);
            instance.Transition(ResourceInstanceStatus.Removing);
        }
        else
        {
            instance.Transition(ResourceInstanceStatus.Provisioning);
        }

        await services.GetRequiredService<IResourceInstanceRepository>().CreateAsync(instance);

        var deployment = Deployment.Create(applicationId, environmentId,
            new CommitId(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20))), "test@example.com").Start();

        if (operation == DeploymentRunOperation.Destroy)
        {
            deployment = deployment.Succeed().StartDestroy();
        }

        await services.GetRequiredService<IDeploymentRepository>().CreateAsync(deployment);

        var run = DeploymentRun.Queue(deployment.Id, operation).Start()
            .IssueToken(token.Hash, DateTime.UtcNow.AddHours(1))
            .RecordPlan("orders", [instance.Id]);
        await services.GetRequiredService<IDeploymentRunRepository>().CreateAsync(run);

        return new PlannedRun(deployment.Id, run, token, instance.Id, resource.Id, environmentId);
    }

    private async Task<(Deployment, DeploymentRun, ResourceInstance, Resource, ResourceDependencyGraph)> LoadAsync(
        PlannedRun seeded)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        return (
            (await services.GetRequiredService<IDeploymentRepository>().GetByIdAsync(seeded.DeploymentId))!,
            (await services.GetRequiredService<IDeploymentRunRepository>().GetByIdAsync(seeded.Run.Id))!,
            (await services.GetRequiredService<IResourceInstanceRepository>().GetByIdAsync(seeded.InstanceId))!,
            (await services.GetRequiredService<IResourceRepository>().GetByIdAsync(seeded.ResourceId))!,
            (await services.GetRequiredService<IResourceDependencyGraphRepository>()
                .GetByEnvironmentAsync(seeded.EnvironmentId))!);
    }

    private sealed record PlannedRun(
        DeploymentId DeploymentId,
        DeploymentRun Run,
        RunnerToken Token,
        ResourceInstanceId InstanceId,
        ResourceId ResourceId,
        EnvironmentId EnvironmentId);

    private async Task<(ApplicationId, EnvironmentId)> SeedApplicationAndEnvironmentAsync(HttpClient client)
    {
        var organisation = await client.CreateOrganisationAsync();
        return await SeedApplicationAndEnvironmentAsync(client, organisation.Id);
    }

    private async Task<(ApplicationId, EnvironmentId)> SeedApplicationAndEnvironmentAsync(HttpClient client,
        Guid organisationId)
    {
        var application = await client.CreateApplicationAsync(organisationId);
        var environmentResponse = await client.PostAsJsonAsync("/environments",
            new CreateEnvironmentRequest(_fixture.Create<string>(), _fixture.Create<string>(),
                new OrganisationId(organisationId)));
        var environment =
            await environmentResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        ArgumentNullException.ThrowIfNull(environment);

        return (new ApplicationId(application.Id), new EnvironmentId(environment.Id));
    }
}
