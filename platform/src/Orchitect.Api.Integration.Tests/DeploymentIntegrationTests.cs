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
        Assert.Equal(nameof(DeploymentStatus.Destroying), body.Status);
        Assert.Equal(DeploymentStatus.Destroying, (await GetDeploymentAsync(deployment.Id)).Status);
        Assert.EndsWith($"{DeploymentsUrl}/{deployment.Id.Value}", response.Headers.Location?.ToString());
        var request = Assert.Single(_queue.Requests);
        Assert.Equal(new DeploymentQueueRequest(deployment.ApplicationId, deployment.Id, DeploymentOperation.Destroy),
            request);
    }

    [Fact]
    public async Task DeploymentApi_WhenDestroyingTwice_ShouldQueueOnceAndReturn409ConflictForSecond()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);

        // Act
        var first = await client.DeleteAsync($"{DeploymentsUrl}/{deployment.Id.Value}");
        var second = await client.DeleteAsync($"{DeploymentsUrl}/{deployment.Id.Value}");

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Single(_queue.Requests);
    }

    [Fact]
    public async Task DeploymentApi_WhenCreatingDeploymentAfterDestroyRequested_ShouldReturn409Conflict()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);
        await client.DeleteAsync($"{DeploymentsUrl}/{deployment.Id.Value}");

        // Act
        var response = await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(deployment.ApplicationId, deployment.EnvironmentId,
                new CommitId(_fixture.Create<string>())));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(_queue.Requests);
    }

    [Fact]
    public async Task DeploymentApi_WhenQueuingDeploymentFails_ShouldReturn500AndFailDeployment()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);
        _queue.Fail = true;

        // Act
        var response = await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(applicationId, environmentId, new CommitId(_fixture.Create<string>())));

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var latest = await GetLatestDeploymentAsync(applicationId, environmentId);
        Assert.Equal(DeploymentStatus.Failed, latest.Status);
    }

    [Fact]
    public async Task DeploymentApi_WhenQueuingDestroyFails_ShouldReturn500AndFailDeployment()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);
        _queue.Fail = true;

        // Act
        var response = await client.DeleteAsync($"{DeploymentsUrl}/{deployment.Id.Value}");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(DeploymentStatus.Failed, (await GetDeploymentAsync(deployment.Id)).Status);
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

    [Fact]
    public async Task DeploymentApi_WhenCreatingDeployment_ShouldReturn202AcceptedAndQueueProvision()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(applicationId, environmentId, new CommitId("abc123")));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var request = Assert.Single(_queue.Requests);
        Assert.Equal(DeploymentOperation.Provision, request.Operation);
    }

    [Theory]
    [InlineData(DeploymentStatus.Pending)]
    [InlineData(DeploymentStatus.Deploying)]
    [InlineData(DeploymentStatus.Destroying)]
    public async Task DeploymentApi_WhenCreatingDeploymentWhileOneIsActive_ShouldReturn409Conflict(
        DeploymentStatus status)
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var active = await SeedDeploymentAsync(client, status);

        // Act
        var response = await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(active.ApplicationId, active.EnvironmentId, new CommitId("abc123")));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(_queue.Requests);
    }

    [Fact]
    public async Task DeploymentApi_WhenRetryingFailedCommit_ShouldAllowSecondFailure()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var commitId = new CommitId("abc123");
        var first = await SeedDeploymentAsync(client, DeploymentStatus.Failed);
        await CreateDeploymentAsync(first.ApplicationId, first.EnvironmentId, DeploymentStatus.Failed, commitId);

        // Act
        var response = await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(first.ApplicationId, first.EnvironmentId, commitId));
        var created = await response.ReadFromJsonAsync<GetDeploymentEndpoint.GetDeploymentResponse>();
        Assert.NotNull(created);
        var retry = await GetDeploymentAsync(new DeploymentId(created.Id));
        await UpdateDeploymentAsync(retry.Start().ProcessDeploymentStatus(1, null));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(DeploymentStatus.Failed, (await GetDeploymentAsync(retry.Id)).Status);
    }

    [Fact]
    public async Task DeploymentApi_WhenSameCommitIsDestroyedTwice_ShouldKeepBothDestroyed()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var commitId = new CommitId("abc123");
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);
        await CreateDeploymentAsync(applicationId, environmentId, DeploymentStatus.Destroyed, commitId);

        // Act
        var second = await CreateDeploymentAsync(applicationId, environmentId, DeploymentStatus.Destroyed, commitId);

        // Assert
        Assert.Equal(DeploymentStatus.Destroyed, (await GetDeploymentAsync(second.Id)).Status);
    }

    [Fact]
    public async Task DeploymentRepository_WhenSecondRunBecomesActive_ShouldThrowActiveDeploymentExists()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployed = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);
        await CreateDeploymentAsync(deployed.ApplicationId, deployed.EnvironmentId, DeploymentStatus.Pending);

        // Act & Assert
        await Assert.ThrowsAsync<ActiveDeploymentExistsException>(() =>
            CreateDeploymentAsync(deployed.ApplicationId, deployed.EnvironmentId, DeploymentStatus.Pending));
        await Assert.ThrowsAsync<ActiveDeploymentExistsException>(() =>
            UpdateDeploymentAsync(deployed.StartDestroy()));
    }

    [Fact]
    public async Task DeploymentRepository_WhenGettingActive_ShouldReturnOnlyActiveUpdatedBefore()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var pending = await SeedDeploymentAsync(client, DeploymentStatus.Pending);
        var deploying = await SeedDeploymentAsync(client, DeploymentStatus.Deploying);
        var destroying = await SeedDeploymentAsync(client, DeploymentStatus.Destroying);
        var deployed = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);
        var failed = await SeedDeploymentAsync(client, DeploymentStatus.Failed);
        var cutoff = DateTime.UtcNow;
        var later = await SeedDeploymentAsync(client, DeploymentStatus.Pending);

        // Act
        IReadOnlyList<Deployment> active;
        using (var scope = _factory.Services.CreateScope())
        {
            active = await scope.ServiceProvider.GetRequiredService<IDeploymentRepository>().GetActiveAsync(cutoff);
        }

        // Assert
        var ids = active.Select(d => d.Id).ToHashSet();
        Assert.Contains(pending.Id, ids);
        Assert.Contains(deploying.Id, ids);
        Assert.Contains(destroying.Id, ids);
        Assert.DoesNotContain(deployed.Id, ids);
        Assert.DoesNotContain(failed.Id, ids);
        Assert.DoesNotContain(later.Id, ids);
    }

    private async Task<(ApplicationId, EnvironmentId)> SeedApplicationAndEnvironmentAsync(HttpClient client)
    {
        var organisation = await client.CreateOrganisationAsync();
        var application = await client.CreateApplicationAsync(organisation.Id);
        var environmentResponse = await client.PostAsJsonAsync("/environments",
            new CreateEnvironmentRequest(_fixture.Create<string>(), _fixture.Create<string>(),
                new OrganisationId(organisation.Id)));
        var environment =
            await environmentResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        ArgumentNullException.ThrowIfNull(environment);

        return (new ApplicationId(application.Id), new EnvironmentId(environment.Id));
    }

    private async Task<Deployment> GetDeploymentAsync(DeploymentId id)
    {
        using var scope = _factory.Services.CreateScope();
        var deployment = await scope.ServiceProvider.GetRequiredService<IDeploymentRepository>().GetByIdAsync(id);
        ArgumentNullException.ThrowIfNull(deployment);
        return deployment;
    }

    private async Task<Deployment> GetLatestDeploymentAsync(ApplicationId applicationId, EnvironmentId environmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var deployment = await scope.ServiceProvider.GetRequiredService<IDeploymentRepository>()
            .GetLatestAsync(applicationId, environmentId);
        ArgumentNullException.ThrowIfNull(deployment);
        return deployment;
    }

    private async Task UpdateDeploymentAsync(Deployment deployment)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDeploymentRepository>().UpdateAsync(deployment);
    }

    private async Task<Deployment> SeedDeploymentAsync(HttpClient client, DeploymentStatus status)
    {
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);

        return await CreateDeploymentAsync(applicationId, environmentId, status);
    }

    private async Task<Deployment> CreateDeploymentAsync(ApplicationId applicationId, EnvironmentId environmentId,
        DeploymentStatus status, CommitId? commitId = null)
    {
        Deployment? created;
        using (var createScope = _factory.Services.CreateScope())
        {
            created = await createScope.ServiceProvider.GetRequiredService<IDeploymentRepository>().CreateAsync(
                Deployment.Create(applicationId, environmentId, commitId ?? new CommitId(_fixture.Create<string>())));
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
            await UpdateDeploymentAsync(deployment);
        }

        return deployment;
    }

    private sealed class CapturingDeploymentQueue : IDeploymentQueue
    {
        public List<DeploymentQueueRequest> Requests { get; } = [];
        public bool Fail { get; set; }

        public Task QueueDeploymentTaskAsync(DeploymentQueueRequest request, CancellationToken token = default)
        {
            if (Fail)
            {
                throw new InvalidOperationException("Queue unavailable.");
            }

            Requests.Add(request);
            return Task.CompletedTask;
        }
    }
}
