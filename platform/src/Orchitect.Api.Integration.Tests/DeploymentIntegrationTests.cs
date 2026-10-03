using System.Net;
using System.Net.Http.Headers;
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
using Orchitect.Engine.Dispatch.Auth;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Queue;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class DeploymentIntegrationTests
{
    private const string DeploymentsUrl = "/deployments";
    private readonly Fixture _fixture = new();
    private readonly CapturingDeploymentQueue _queue = new();
    private readonly SignallingExecutor _executor = new();
    private readonly WebApplicationFactory<Program> _factory;

    public DeploymentIntegrationTests(WebApplicationFactoryWithPostgres factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDeploymentQueue>();
            services.AddSingleton<IDeploymentQueue>(_queue);
            services.RemoveAll<IExecutor>();
            services.AddSingleton<IExecutor>(_executor);
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
    public async Task DeploymentApi_WhenCancellingQueuedDeployment_ShouldCancelDeploymentAndRun()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var (applicationId, environmentId) = await SeedApplicationAndEnvironmentAsync(client);
        await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(applicationId, environmentId, NewCommitId()));
        var request = Assert.Single(_queue.Requests);

        // Act
        var response = await client.PostAsync($"{DeploymentsUrl}/{request.DeploymentId.Value}/cancel", null);
        var body = await response.ReadFromJsonAsync<CancelDeploymentEndpoint.CancelDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(request.DeploymentId.Value, body.Id);
        Assert.Equal(nameof(DeploymentStatus.Cancelled), body.Status);
        Assert.Equal(request.RunId.Value, body.RunId);
        Assert.Equal(nameof(DeploymentRunStatus.Cancelled), body.RunStatus);
        Assert.EndsWith($"{DeploymentsUrl}/{request.DeploymentId.Value}", response.Headers.Location?.ToString());
        var deployment = await GetDeploymentAsync(request.DeploymentId);
        Assert.Equal(DeploymentStatus.Cancelled, deployment.Status);
        Assert.NotNull(deployment.CompletedAt);
        var run = await GetLatestRunAsync(request.DeploymentId);
        Assert.Equal(DeploymentRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.FinishedAt);
    }

    [Fact]
    public async Task DeploymentApi_WhenCreatingDeploymentAfterCancel_ShouldReturn202Accepted()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var active = await SeedDeploymentWithRunAsync(client, DeploymentStatus.Pending, DeploymentRunStatus.Queued);
        await client.PostAsync($"{DeploymentsUrl}/{active.Id.Value}/cancel", null);

        // Act
        var response = await client.PostAsJsonAsync(DeploymentsUrl,
            new CreateDeploymentRequest(active.ApplicationId, active.EnvironmentId, NewCommitId()));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task DeploymentApi_WhenCancellingQueuedDestroy_ShouldCancelAndAllowDestroyAgain()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Deployed);
        await client.DeleteAsync($"{DeploymentsUrl}/{deployment.Id.Value}");

        // Act
        var response = await client.PostAsync($"{DeploymentsUrl}/{deployment.Id.Value}/cancel", null);
        var destroyAgain = await client.DeleteAsync($"{DeploymentsUrl}/{deployment.Id.Value}");

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, destroyAgain.StatusCode);
        Assert.Equal(2, _queue.Requests.Count);
        using var scope = _factory.Services.CreateScope();
        var cancelledRun = await scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>()
            .GetByIdAsync(_queue.Requests[0].RunId);
        Assert.Equal(DeploymentRunOperation.Destroy, cancelledRun?.Operation);
        Assert.Equal(DeploymentRunStatus.Cancelled, cancelledRun?.Status);
    }

    [Fact]
    public async Task DeploymentApi_WhenCancellingRunningRun_ShouldReturn202AcceptedAndStopRunner()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment =
            await SeedDeploymentWithRunAsync(client, DeploymentStatus.Deploying, DeploymentRunStatus.Running);
        var run = await GetLatestRunAsync(deployment.Id);
        using var tracked = _factory.Services.GetRequiredService<IDeploymentRunCancellation>().Track(run.Id);

        // Act
        var response = await client.PostAsync($"{DeploymentsUrl}/{deployment.Id.Value}/cancel", null);
        var body = await response.ReadFromJsonAsync<CancelDeploymentEndpoint.CancelDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(nameof(DeploymentStatus.Deploying), body.Status);
        Assert.Equal(run.Id.Value, body.RunId);
        Assert.Equal(nameof(DeploymentRunStatus.Running), body.RunStatus);
        Assert.NotNull(body.CancelRequestedAt);
        Assert.True(tracked.StopRequested.IsCancellationRequested);
        Assert.Empty(_executor.Signalled);
        Assert.NotNull((await GetLatestRunAsync(deployment.Id)).CancelRequestedAt);
    }

    [Fact]
    public async Task DeploymentApi_WhenCancellingRunningRunNotInThisProcess_ShouldRecordRequestAndSignalRunner()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment =
            await SeedDeploymentWithRunAsync(client, DeploymentStatus.Deploying, DeploymentRunStatus.Running);
        var run = await GetLatestRunAsync(deployment.Id);

        // Act
        var response = await client.PostAsync($"{DeploymentsUrl}/{deployment.Id.Value}/cancel", null);
        var body = await response.ReadFromJsonAsync<CancelDeploymentEndpoint.CancelDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(body?.CancelRequestedAt);
        Assert.Equal([run.Id.Value.ToString()], _executor.Signalled);
        Assert.Equal(DeploymentStatus.Deploying, (await GetDeploymentAsync(deployment.Id)).Status);
        var stored = await GetLatestRunAsync(deployment.Id);
        Assert.Equal(DeploymentRunStatus.Running, stored.Status);
        Assert.NotNull(stored.CancelRequestedAt);
        Assert.Equal(body.CancelRequestedAt.Value, stored.CancelRequestedAt.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task DeploymentApi_WhenGettingDeploymentAfterCancelRequested_ShouldReturnCancelRequestedAt()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment =
            await SeedDeploymentWithRunAsync(client, DeploymentStatus.Deploying, DeploymentRunStatus.Running);
        await client.PostAsync($"{DeploymentsUrl}/{deployment.Id.Value}/cancel", null);

        // Act
        var response = await client.GetAsync($"{DeploymentsUrl}/{deployment.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetDeploymentEndpoint.GetDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.LatestRun?.CancelRequestedAt);
    }

    [Fact]
    public async Task DeploymentRunRepository_WhenUpdatingStaleRun_ShouldThrowConflict()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment =
            await SeedDeploymentWithRunAsync(client, DeploymentStatus.Pending, DeploymentRunStatus.Queued);
        var stale = await GetLatestRunAsync(deployment.Id);
        using var scope = _factory.Services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>();
        var started = await runs.UpdateAsync(stale.Start());

        // Act & Assert
        await Assert.ThrowsAsync<DeploymentRunConflictException>(() => runs.UpdateAsync(stale.Cancel()));
        Assert.NotNull(started);
        Assert.NotEqual(stale.Version, started.Version);
        await runs.UpdateAsync(started.RequestCancel(DateTime.UtcNow));
        Assert.Equal(DeploymentRunStatus.Running, (await GetLatestRunAsync(deployment.Id)).Status);
    }

    [Fact]
    public async Task DeploymentApi_WhenQueuedRunIsClaimedWhileCancelling_ShouldRequestStopInstead()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment =
            await SeedDeploymentWithRunAsync(client, DeploymentStatus.Pending, DeploymentRunStatus.Queued);
        var run = await GetLatestRunAsync(deployment.Id);
        var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IDeploymentRunRepository>(sp => new ClaimingRunRepository(
                ActivatorUtilities.CreateInstance<Persistence.Repositories.Engine.DeploymentRunRepository>(sp)));
        }));
        var claimingClient = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await claimingClient.PostAsync($"{DeploymentsUrl}/{deployment.Id.Value}/cancel", null);
        var body = await response.ReadFromJsonAsync<CancelDeploymentEndpoint.CancelDeploymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(nameof(DeploymentStatus.Pending), body?.Status);
        Assert.Equal(nameof(DeploymentRunStatus.Running), body?.RunStatus);
        Assert.NotNull(body?.CancelRequestedAt);
        Assert.Equal([run.Id.Value.ToString()], _executor.Signalled);
        Assert.Equal(DeploymentStatus.Pending, (await GetDeploymentAsync(deployment.Id)).Status);
    }

    [Theory]
    [InlineData(DeploymentStatus.Deployed, DeploymentRunStatus.Succeeded)]
    [InlineData(DeploymentStatus.Failed, DeploymentRunStatus.Failed)]
    [InlineData(DeploymentStatus.Deployed, null)]
    public async Task DeploymentApi_WhenCancellingDeploymentWithoutActiveRun_ShouldReturn409Conflict(
        DeploymentStatus status, DeploymentRunStatus? runStatus)
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentWithRunAsync(client, status, runStatus);

        // Act
        var response = await client.PostAsync($"{DeploymentsUrl}/{deployment.Id.Value}/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(status, (await GetDeploymentAsync(deployment.Id)).Status);
    }

    [Fact]
    public async Task DeploymentApi_WhenCancellingNonExistentDeployment_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.PostAsync($"{DeploymentsUrl}/{Guid.NewGuid()}/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
    public async Task DeploymentRunRepository_WhenGettingByTokenHash_ShouldReturnRunUntilRevoked()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Deploying);
        var token = RunnerToken.Generate();
        var run = DeploymentRun.Queue(deployment.Id, DeploymentRunOperation.Provision).Start()
            .IssueToken(token.Hash, DateTime.UtcNow.AddHours(1));
        using var scope = _factory.Services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>();
        await runs.CreateAsync(run);

        // Act
        var issued = await runs.GetByTokenHashAsync(token.Hash);
        await runs.UpdateAsync(run.Complete(0, null));
        var revoked = await runs.GetByTokenHashAsync(token.Hash);

        // Assert
        Assert.Equal(run.Id, issued?.Id);
        Assert.Null(revoked);
    }

    [Fact]
    public async Task DeploymentApi_WhenUsingRunnerToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = await _factory.CreateClient().AddAuthorisationHeader();
        var deployment = await SeedDeploymentAsync(client, DeploymentStatus.Deploying);
        var token = RunnerToken.Generate();
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>().CreateAsync(
                DeploymentRun.Queue(deployment.Id, DeploymentRunOperation.Provision).Start()
                    .IssueToken(token.Hash, DateTime.UtcNow.AddHours(1)));
        }

        var runnerClient = _factory.CreateClient();
        runnerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        // Act
        var response = await runnerClient.GetAsync($"{DeploymentsUrl}/{deployment.Id.Value}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private async Task<Deployment> SeedDeploymentWithRunAsync(HttpClient client, DeploymentStatus status,
        DeploymentRunStatus? runStatus)
    {
        var deployment = await SeedDeploymentAsync(client, status);

        if (runStatus is null)
        {
            return deployment;
        }

        var queued = DeploymentRun.Queue(deployment.Id, DeploymentRunOperation.Provision);
        var run = runStatus switch
        {
            DeploymentRunStatus.Queued => queued,
            DeploymentRunStatus.Running => queued.Start(),
            DeploymentRunStatus.Succeeded => queued.Start().Complete(0, null),
            DeploymentRunStatus.Failed => queued.Start().Complete(1, null),
            _ => throw new ArgumentOutOfRangeException(nameof(runStatus), runStatus, null)
        };

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDeploymentRunRepository>().CreateAsync(run);

        return deployment;
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

    private sealed class SignallingExecutor : IExecutor
    {
        public List<string> Signalled { get; } = [];

        public Task<ExecutorResult> ExecuteAsync(ExecutorContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> SignalStopAsync(string runId, CancellationToken cancellationToken = default)
        {
            Signalled.Add(runId);
            return Task.FromResult(true);
        }
    }

    private sealed class ClaimingRunRepository(IDeploymentRunRepository inner) : IDeploymentRunRepository
    {
        private bool _claimed;

        public async Task<DeploymentRun?> UpdateAsync(DeploymentRun run, CancellationToken cancellationToken = default)
        {
            if (!_claimed && run.Status == DeploymentRunStatus.Cancelled)
            {
                _claimed = true;
                var current = await inner.GetByIdAsync(run.Id, cancellationToken);
                await inner.UpdateAsync(current!.Start(), cancellationToken);
            }

            return await inner.UpdateAsync(run, cancellationToken);
        }

        public Task<DeploymentRun?> GetLatestAsync(DeploymentId deploymentId,
            CancellationToken cancellationToken = default) => inner.GetLatestAsync(deploymentId, cancellationToken);

        public Task<DeploymentRun?> GetByTokenHashAsync(string tokenHash,
            CancellationToken cancellationToken = default) => inner.GetByTokenHashAsync(tokenHash, cancellationToken);

        public Task<DeploymentRun?> CreateAsync(DeploymentRun run, CancellationToken cancellationToken = default) =>
            inner.CreateAsync(run, cancellationToken);

        public IEnumerable<DeploymentRun> GetAll() => inner.GetAll();

        public Task<DeploymentRun?> GetByIdAsync(DeploymentRunId id, CancellationToken cancellationToken = default) =>
            inner.GetByIdAsync(id, cancellationToken);
        public Task LockAsync(DeploymentRunId id, CancellationToken cancellationToken = default) =>
            inner.LockAsync(id, cancellationToken);
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
