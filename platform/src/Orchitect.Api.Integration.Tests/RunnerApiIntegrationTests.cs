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
        var body = await response.Content.ReadFromJsonAsync<RunDescriptor>(RunnerContract.JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
}
