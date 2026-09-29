using System.Net;
using AutoFixture;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Api.Endpoints.Engine.Deployment;
using Orchitect.Api.Endpoints.Engine.Environment;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Infrastructure.Engine.Queue;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class DeploymentIntegrationTests
{
    private const string DeploymentsUrl = "/deployments";
    private readonly Fixture _fixture = new();
    private readonly CapturingDeploymentQueue _queue = new();
    private readonly WebApplicationFactory<Program> _factory;

    public DeploymentIntegrationTests(WebApplicationFactoryWithPostgres factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDeploymentQueue>();
            services.AddSingleton<IDeploymentQueue>(_queue);
        }));
    }

    [Fact]
    public async Task DeploymentApi_WhenGettingDeploymentById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);

        // Act
        var response = await client.GetAsync($"{DeploymentsUrl}/{deployment.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetDeploymentEndpoint.GetDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(deployment.Id.Value, body.Id);
        Assert.Equal(deployment.CommitId.Value, body.CommitId);
        Assert.Equal(nameof(DeploymentStatus.Deployed), body.Status);
    }

    [Fact]
    public async Task DeploymentApi_WhenGettingDeploymentByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{DeploymentsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(DeploymentStatus.Deployed)]
    [InlineData(DeploymentStatus.Failed)]
    public async Task DeploymentApi_WhenDestroyingLatestFinishedDeployment_ShouldReturn202AcceptedAndQueueDestroy(
        DeploymentStatus status)
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, status);

        // Act
        var response = await client.DeleteAsync($"{DeploymentsUrl}/{deployment.Id.Value}");
        var body = await response.ReadFromJsonAsync<DestroyDeploymentEndpoint.DestroyDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(deployment.Id.Value, body.Id);
        Assert.EndsWith($"{DeploymentsUrl}/{deployment.Id.Value}", response.Headers.Location?.ToString());
        var request = Assert.Single(_queue.Requests);
        Assert.Equal(new DeploymentQueueRequest(deployment.ApplicationId, deployment.Id, DeploymentOperation.Destroy),
            request);
    }

    [Fact]
    public async Task DeploymentApi_WhenDestroyingNonExistentDeployment_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.DeleteAsync($"{DeploymentsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_queue.Requests);
    }

    [Theory]
    [InlineData(DeploymentStatus.Pending)]
    [InlineData(DeploymentStatus.Deploying)]
    [InlineData(DeploymentStatus.Destroying)]
    [InlineData(DeploymentStatus.Destroyed)]
    public async Task DeploymentApi_WhenDestroyingUnfinishedOrDestroyedDeployment_ShouldReturn409Conflict(
        DeploymentStatus status)
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, status);

        // Act
        var response = await client.DeleteAsync($"{DeploymentsUrl}/{deployment.Id.Value}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(_queue.Requests);
    }

    [Fact]
    public async Task DeploymentApi_WhenDestroyingOlderDeployment_ShouldReturn409Conflict()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var older = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);
        await CreateDeploymentAsync(older.ApplicationId, older.EnvironmentId, DeploymentStatus.Deployed);

        // Act
        var response = await client.DeleteAsync($"{DeploymentsUrl}/{older.Id.Value}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(_queue.Requests);
    }

    private async Task<Deployment> SeedDeploymentAsync(HttpClient client, DeploymentStatus status)
    {
        var organisation = await client.CreateOrganisationAsync();
        var application = await client.CreateApplicationAsync(organisation.Id);
        var environmentResponse = await client.PostAsJsonAsync("/environments",
            new CreateEnvironmentRequest(_fixture.Create<string>(), _fixture.Create<string>(),
                new OrganisationId(organisation.Id)));
        var environment =
            await environmentResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        ArgumentNullException.ThrowIfNull(environment);

        return await CreateDeploymentAsync(new ApplicationId(application.Id), new EnvironmentId(environment.Id),
            status);
    }

    private async Task<Deployment> CreateDeploymentAsync(ApplicationId applicationId, EnvironmentId environmentId,
        DeploymentStatus status)
    {
        Deployment? created;
        using (var createScope = _factory.Services.CreateScope())
        {
            created = await createScope.ServiceProvider.GetRequiredService<IDeploymentRepository>().CreateAsync(
                Deployment.Create(applicationId, environmentId, new CommitId(_fixture.Create<string>())));
        }

        ArgumentNullException.ThrowIfNull(created);

        var deployment = status switch
        {
            DeploymentStatus.Pending => created,
            DeploymentStatus.Deploying => created.Start(),
            DeploymentStatus.Deployed => created.Start().ProcessDeploymentStatus(0, null),
            DeploymentStatus.Failed => created.Start().ProcessDeploymentStatus(1, null),
            DeploymentStatus.Destroying => created.Start().ProcessDeploymentStatus(0, null).StartDestroy(),
            DeploymentStatus.Destroyed => created.Start().ProcessDeploymentStatus(0, null).StartDestroy()
                .ProcessDeploymentStatus(0, null),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

        if (deployment != created)
        {
            using var updateScope = _factory.Services.CreateScope();
            await updateScope.ServiceProvider.GetRequiredService<IDeploymentRepository>().UpdateAsync(deployment);
        }

        return deployment;
    }

    private sealed class CapturingDeploymentQueue : IDeploymentQueue
    {
        public List<DeploymentQueueRequest> Requests { get; } = [];

        public Task QueueDeploymentTaskAsync(DeploymentQueueRequest request, CancellationToken token = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }
}
