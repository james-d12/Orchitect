using System.Net;
using System.Security.Cryptography;
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
using Orchitect.Engine.Dispatch.Queue;
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
        Assert.Equal(deployment.RequestedBy, body.RequestedBy);
        Assert.NotNull(body.StartedAt);
        Assert.NotNull(body.CompletedAt);
        Assert.Null(body.ErrorSummary);
    }

    [Fact]
    public async Task DeploymentApi_WhenGettingFailedDeployment_ShouldReturnErrorSummary()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Failed);

        // Act
        var response = await client.GetAsync($"{DeploymentsUrl}/{deployment.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetDeploymentEndpoint.GetDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(nameof(DeploymentStatus.Failed), body.Status);
        Assert.NotNull(body.CompletedAt);
        Assert.Equal("The runner exited with code 1.", body.ErrorSummary);
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
        var run = await GetLatestRunAsync(deployment.Id);
        Assert.Equal(new DeploymentQueueRequest(run.Id, deployment.ApplicationId, deployment.Id), request);
        Assert.Equal(DeploymentRunOperation.Destroy, run.Operation);
        Assert.Equal(DeploymentRunStatus.Queued, run.Status);
    }

    [Fact]
    public async Task DeploymentApi_WhenGettingDeploymentAfterDestroy_ShouldReturnDestroyRunAsLatest()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);
        var created = await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(applicationId, environmentId, NewCommitId()));
        var deploymentId = Assert.Single(_queue.Requests).DeploymentId;
        var provisionRun = await GetLatestRunAsync(deploymentId);
        await UpdateDeploymentAsync((await GetDeploymentAsync(deploymentId)).Start().ProcessDeploymentStatus(0, null));
        await client.DeleteAsync($"{DeploymentsUrl}/{deploymentId.Value}");

        // Act
        var response = await client.GetAsync($"{DeploymentsUrl}/{deploymentId.Value}");
        var body = await response.ReadFromJsonAsync<GetDeploymentEndpoint.GetDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.LatestRun);
        Assert.Equal(2, _queue.Requests.Count);
        Assert.Equal(_queue.Requests[1].RunId.Value, body.LatestRun.Id);
        Assert.NotEqual(provisionRun.Id.Value, body.LatestRun.Id);
        Assert.Equal(nameof(DeploymentRunOperation.Destroy), body.LatestRun.Operation);
        Assert.Equal(nameof(DeploymentRunStatus.Queued), body.LatestRun.Status);
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
            new CreateDeploymentRequest(deployment.ApplicationId, deployment.EnvironmentId, NewCommitId()));

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
            new CreateDeploymentRequest(applicationId, environmentId, NewCommitId()));

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var latest = await GetLatestDeploymentAsync(applicationId, environmentId);
        Assert.Equal(DeploymentStatus.Failed, latest.Status);
        Assert.NotNull(latest.CompletedAt);
        Assert.Equal("The deployment could not be queued.", latest.ErrorSummary);
        var run = await GetLatestRunAsync(latest.Id);
        Assert.Equal(DeploymentRunStatus.Failed, run.Status);
        Assert.Equal("The deployment could not be queued.", run.ErrorSummary);
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
        var failed = await GetDeploymentAsync(deployment.Id);
        Assert.Equal(DeploymentStatus.Failed, failed.Status);
        Assert.Equal("The destroy could not be queued.", failed.ErrorSummary);
        var run = await GetLatestRunAsync(deployment.Id);
        Assert.Equal(DeploymentRunOperation.Destroy, run.Operation);
        Assert.Equal(DeploymentRunStatus.Failed, run.Status);
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
            new CreateDeploymentRequest(applicationId, environmentId, NewCommitId()));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var request = Assert.Single(_queue.Requests);
        var deployment = await GetDeploymentAsync(request.DeploymentId);
        Assert.Equal("test@example.com", deployment.RequestedBy);
        Assert.Null(deployment.StartedAt);
        var run = await GetLatestRunAsync(request.DeploymentId);
        Assert.Equal(request.RunId, run.Id);
        Assert.Equal(DeploymentRunOperation.Provision, run.Operation);
        Assert.Equal(DeploymentRunStatus.Queued, run.Status);
    }

    [Fact]
    public async Task DeploymentApi_WhenGettingCreatedDeployment_ShouldReturnLatestRun()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);
        await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(applicationId, environmentId, NewCommitId()));
        var request = Assert.Single(_queue.Requests);

        // Act
        var response = await client.GetAsync($"{DeploymentsUrl}/{request.DeploymentId.Value}");
        var body = await response.ReadFromJsonAsync<GetDeploymentEndpoint.GetDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.LatestRun);
        Assert.Equal(request.RunId.Value, body.LatestRun.Id);
        Assert.Equal(nameof(DeploymentRunOperation.Provision), body.LatestRun.Operation);
        Assert.Equal(nameof(DeploymentRunStatus.Queued), body.LatestRun.Status);
        Assert.Null(body.LatestRun.StartedAt);
        Assert.Null(body.LatestRun.ExitCode);
    }

    [Fact]
    public async Task DeploymentRunRepository_WhenUpdatingRun_ShouldPersistResult()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);
        var run = DeploymentRun.Queue(deployment.Id, DeploymentRunOperation.Provision);

        // Act
        using (var scope = _factory.Services.CreateScope())
        {
            var runs = scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>();
            await runs.CreateAsync(run);
            await runs.UpdateAsync(run.Start().Complete(1, null, "container-1"));
        }

        // Assert
        var stored = await GetLatestRunAsync(deployment.Id);
        Assert.Equal(run.Id, stored.Id);
        Assert.Equal(DeploymentRunStatus.Failed, stored.Status);
        Assert.Equal(1, stored.ExitCode);
        Assert.Equal("container-1", stored.RunnerId);
        Assert.Equal("The runner exited with code 1.", stored.ErrorSummary);
        Assert.NotNull(stored.StartedAt);
        Assert.NotNull(stored.FinishedAt);
    }

    [Fact]
    public async Task DeploymentApi_WhenGettingDeploymentWithoutRuns_ShouldReturnNullLatestRun()
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
        Assert.Null(body.LatestRun);
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("main")]
    [InlineData("")]
    public async Task DeploymentApi_WhenCreatingDeploymentWithInvalidCommitId_ShouldReturn400BadRequest(
        string commitId)
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(applicationId, environmentId, new CommitId(commitId)));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_queue.Requests);
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
            new CreateDeploymentRequest(active.ApplicationId, active.EnvironmentId, NewCommitId()));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(_queue.Requests);
    }

    [Fact]
    public async Task DeploymentApi_WhenRetryingFailedCommit_ShouldAllowSecondFailure()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var commitId = NewCommitId();
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
        var commitId = NewCommitId();
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

    private async Task<DeploymentRun> GetLatestRunAsync(DeploymentId deploymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>()
            .GetLatestAsync(deploymentId);
        ArgumentNullException.ThrowIfNull(run);
        return run;
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

    private static CommitId NewCommitId() => new(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20)));

    private async Task<Deployment> CreateDeploymentAsync(ApplicationId applicationId, EnvironmentId environmentId,
        DeploymentStatus status, CommitId? commitId = null)
    {
        Deployment? created;
        using (var createScope = _factory.Services.CreateScope())
        {
            created = await createScope.ServiceProvider.GetRequiredService<IDeploymentRepository>().CreateAsync(
                Deployment.Create(applicationId, environmentId, commitId ?? NewCommitId(), "test@example.com"));
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
