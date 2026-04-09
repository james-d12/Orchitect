using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Engine.Application;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Engine.Application;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class ApplicationIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string ApplicationsUrl = "/applications";
    private readonly Fixture _fixture = new();

    private CreateApplicationRequest BuildCreateRequest(Guid organisationId) =>
        new(_fixture.Create<string>(),
            organisationId.ToString(),
            new CreateRepositoryRequest(
                _fixture.Create<string>(),
                new Uri("https://github.com/test/repo"),
                _fixture.Create<RepositoryProvider>()));

    private UpdateApplicationEndpoint.UpdateApplicationRequest BuildUpdateRequest() =>
        new(_fixture.Create<string>(),
            new UpdateApplicationEndpoint.UpdateRepositoryRequest(
                _fixture.Create<string>(),
                new Uri("https://github.com/test/updated-repo"),
                _fixture.Create<RepositoryProvider>()));

    [Fact]
    public async Task ApplicationApi_WhenCreatingApplication_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var request = BuildCreateRequest(organisation.Id);

        // Act
        var response = await client.PostAsJsonAsync(ApplicationsUrl, request);
        var body = await response.ReadFromJsonAsync<CreateApplicationEndpoint.CreateApplicationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.IsType<Guid>(body.Id);
    }

    [Fact]
    public async Task ApplicationApi_WhenGettingApplicationById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createRequest = BuildCreateRequest(organisation.Id);
        var createResponse = await client.PostAsJsonAsync(ApplicationsUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateApplicationEndpoint.CreateApplicationResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync($"{ApplicationsUrl}/{created.Id}");
        var body = await response.ReadFromJsonAsync<GetApplicationEndpoint.GetApplicationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(createRequest.Name, body.Name);
        Assert.Equal(createRequest.Repository.Name, body.RepositoryName);
        Assert.Equal(createRequest.Repository.Url.ToString(), body.RepositoryUrl);
        Assert.NotEqual(default, body.CreatedAt);
        Assert.NotEqual(default, body.UpdatedAt);
    }

    [Fact]
    public async Task ApplicationApi_WhenGettingApplicationByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{ApplicationsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationApi_WhenGettingAllApplications_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createRequest = BuildCreateRequest(organisation.Id);
        var createResponse = await client.PostAsJsonAsync(ApplicationsUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateApplicationEndpoint.CreateApplicationResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync(ApplicationsUrl);
        var body = await response.ReadFromJsonAsync<GetAllApplicationsEndpoint.GetAllApplicationsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Applications);

        var application = body.Applications.Single(a => a.Id == created.Id);
        Assert.Equal(createRequest.Name, application.Name);
        Assert.Equal(createRequest.Repository.Name, application.RepositoryName);
        Assert.Equal(createRequest.Repository.Url.ToString(), application.RepositoryUrl);
        Assert.NotEqual(default, application.CreatedAt);
        Assert.NotEqual(default, application.UpdatedAt);
    }

    [Fact]
    public async Task ApplicationApi_WhenUpdatingApplication_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var created = await client.CreateApplicationAsync(organisation.Id);

        var updateRequest = BuildUpdateRequest();

        // Act
        var response = await client.PutAsJsonAsync($"{ApplicationsUrl}/{created.Id}", updateRequest);
        var body = await response.ReadFromJsonAsync<UpdateApplicationEndpoint.UpdateApplicationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(updateRequest.Name, body.Name);
        Assert.Equal(updateRequest.Repository.Name, body.Repository.Name);
        Assert.Equal(updateRequest.Repository.Url, body.Repository.Url);
        Assert.Equal(updateRequest.Repository.Provider, body.Repository.Provider);
        Assert.NotEqual(default, body.CreatedAt);
        Assert.NotEqual(default, body.UpdatedAt);
    }

    [Fact]
    public async Task ApplicationApi_WhenUpdatingNonExistentApplication_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.PutAsJsonAsync($"{ApplicationsUrl}/{Guid.NewGuid()}", BuildUpdateRequest());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationApi_WhenDeletingApplication_ShouldReturn204NoContent()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var created = await client.CreateApplicationAsync(organisation.Id);

        // Act
        var response = await client.DeleteAsync($"{ApplicationsUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationApi_WhenDeletingNonExistentApplication_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.DeleteAsync($"{ApplicationsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationApi_WhenGettingApplicationAfterDeletion_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var created = await client.CreateApplicationAsync(organisation.Id);

        await client.DeleteAsync($"{ApplicationsUrl}/{created.Id}");

        // Act
        var response = await client.GetAsync($"{ApplicationsUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}