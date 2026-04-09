using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Engine.Environment;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class EnvironmentIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string EnvironmentsUrl = "/environments";
    private readonly Fixture _fixture = new();

    private CreateEnvironmentRequest BuildCreateRequest(Guid organisationId) =>
        new(_fixture.Create<string>(),
            _fixture.Create<string>(),
            new OrganisationId(organisationId));

    private UpdateEnvironmentEndpoint.UpdateEnvironmentRequest BuildUpdateRequest() =>
        new(_fixture.Create<string>(), _fixture.Create<string>());

    [Fact]
    public async Task EnvironmentApi_WhenCreatingEnvironment_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var request = BuildCreateRequest(organisation.Id);

        // Act
        var response = await client.PostAsJsonAsync(EnvironmentsUrl, request);
        var body = await response.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.IsType<Guid>(body.Id);
    }

    [Fact]
    public async Task EnvironmentApi_WhenGettingEnvironmentById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createRequest = BuildCreateRequest(organisation.Id);
        var createResponse = await client.PostAsJsonAsync(EnvironmentsUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync($"{EnvironmentsUrl}/{created.Id}");
        var body = await response.ReadFromJsonAsync<GetEnvironmentEndpoint.GetEnvironmentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(createRequest.Name, body.Name);
    }

    [Fact]
    public async Task EnvironmentApi_WhenGettingEnvironmentByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{EnvironmentsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EnvironmentApi_WhenGettingAllEnvironments_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createRequest = BuildCreateRequest(organisation.Id);
        var createResponse = await client.PostAsJsonAsync(EnvironmentsUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync(EnvironmentsUrl);
        var body = await response.ReadFromJsonAsync<GetAllEnvironmentsEndpoint.GetAllEnvironmentsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Environments);

        var environment = body.Environments.Single(e => e.Id == created.Id);
        Assert.Equal(createRequest.Name, environment.Name);
    }

    [Fact]
    public async Task EnvironmentApi_WhenUpdatingEnvironment_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createResponse = await client.PostAsJsonAsync(EnvironmentsUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        Assert.NotNull(created);

        var updateRequest = BuildUpdateRequest();

        // Act
        var response = await client.PutAsJsonAsync($"{EnvironmentsUrl}/{created.Id}", updateRequest);
        var body = await response.ReadFromJsonAsync<UpdateEnvironmentEndpoint.UpdateEnvironmentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(updateRequest.Name, body.Name);
        Assert.Equal(updateRequest.Description, body.Description);
        Assert.NotEqual(default, body.CreatedAt);
        Assert.NotEqual(default, body.UpdatedAt);
    }

    [Fact]
    public async Task EnvironmentApi_WhenUpdatingNonExistentEnvironment_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.PutAsJsonAsync($"{EnvironmentsUrl}/{Guid.NewGuid()}", BuildUpdateRequest());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EnvironmentApi_WhenDeletingEnvironment_ShouldReturn204NoContent()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createResponse = await client.PostAsJsonAsync(EnvironmentsUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.DeleteAsync($"{EnvironmentsUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task EnvironmentApi_WhenDeletingNonExistentEnvironment_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.DeleteAsync($"{EnvironmentsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EnvironmentApi_WhenGettingEnvironmentAfterDeletion_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createResponse = await client.PostAsJsonAsync(EnvironmentsUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CreateEnvironmentEndpoint.CreateEnvironmentResponse>();
        Assert.NotNull(created);

        await client.DeleteAsync($"{EnvironmentsUrl}/{created.Id}");

        // Act
        var response = await client.GetAsync($"{EnvironmentsUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
