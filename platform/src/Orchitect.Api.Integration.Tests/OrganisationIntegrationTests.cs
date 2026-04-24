using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Core.Organisation;
using Orchitect.Api.Integration.Tests.Helpers;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class OrganisationIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string OrganisationsUrl = "/organisations";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task OrganisationApi_WhenCreatingOrganisation_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var request = _fixture.Create<CreateOrganisationEndpoint.CreateOrganisationRequest>();

        // Act
        var response = await client.PostAsJsonAsync(OrganisationsUrl, request);
        var body = await response.ReadFromJsonAsync<CreateOrganisationEndpoint.CreateOrganisationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.IsType<Guid>(body.Id);
        Assert.Equal(request.Name, body.Name);
    }

    [Fact]
    public async Task OrganisationApi_WhenGettingOrganisationById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var createRequest = _fixture.Create<CreateOrganisationEndpoint.CreateOrganisationRequest>();
        var createResponse = await client.PostAsJsonAsync(OrganisationsUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateOrganisationEndpoint.CreateOrganisationResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync($"{OrganisationsUrl}/{created.Id}");
        var body = await response.ReadFromJsonAsync<GetOrganisationEndpoint.GetOrganisationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(created.Name, body.Name);
    }

    [Fact]
    public async Task OrganisationApi_WhenGettingOrganisationByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{OrganisationsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrganisationApi_WhenGettingAllOrganisations_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var createRequest = _fixture.Create<CreateOrganisationEndpoint.CreateOrganisationRequest>();
        await client.PostAsJsonAsync(OrganisationsUrl, createRequest);

        // Act
        var response = await client.GetAsync(OrganisationsUrl);
        var body = await response.ReadFromJsonAsync<GetAllOrganisationsEndpoint.GetAllOrganisationsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Organisations);
    }

    [Fact]
    public async Task OrganisationApi_WhenUpdatingOrganisation_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var createRequest = _fixture.Create<CreateOrganisationEndpoint.CreateOrganisationRequest>();
        var createResponse = await client.PostAsJsonAsync(OrganisationsUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateOrganisationEndpoint.CreateOrganisationResponse>();
        Assert.NotNull(created);

        var updatedName = _fixture.Create<string>();

        // Act
        var response = await client.PutAsJsonAsync($"{OrganisationsUrl}/{created.Id}", new UpdateOrganisationEndpoint.UpdateOrganisationRequest(updatedName));
        var body = await response.ReadFromJsonAsync<UpdateOrganisationEndpoint.UpdateOrganisationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(updatedName, body.Name);
    }

    [Fact]
    public async Task OrganisationApi_WhenUpdatingNonExistentOrganisation_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.PutAsJsonAsync($"{OrganisationsUrl}/{Guid.NewGuid()}", new UpdateOrganisationEndpoint.UpdateOrganisationRequest("DoesNotMatter"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrganisationApi_WhenDeletingOrganisation_ShouldReturn204NoContent()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var createRequest = _fixture.Create<CreateOrganisationEndpoint.CreateOrganisationRequest>();
        var createResponse = await client.PostAsJsonAsync(OrganisationsUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateOrganisationEndpoint.CreateOrganisationResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.DeleteAsync($"{OrganisationsUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task OrganisationApi_WhenDeletingNonExistentOrganisation_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.DeleteAsync($"{OrganisationsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrganisationApi_WhenGettingOrganisationAfterDeletion_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var createRequest = _fixture.Create<CreateOrganisationEndpoint.CreateOrganisationRequest>();
        var createResponse = await client.PostAsJsonAsync(OrganisationsUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateOrganisationEndpoint.CreateOrganisationResponse>();
        Assert.NotNull(created);

        await client.DeleteAsync($"{OrganisationsUrl}/{created.Id}");

        // Act
        var response = await client.GetAsync($"{OrganisationsUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

}