using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Engine.ResourceTemplate;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class ResourceTemplateIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string ResourceTemplatesUrl = "/resource-templates";
    private readonly Fixture _fixture = new();

    private CreateResourceTemplateRequest BuildCreateRequest(Guid organisationId) =>
        new()
        {
            OrganisationId = new OrganisationId(organisationId),
            Name = _fixture.Create<string>(),
            Type = _fixture.Create<string>(),
            Description = _fixture.Create<string>(),
            Provider = _fixture.Create<ResourceTemplateProvider>()
        };

    private CreateResourceTemplateWithVersionRequest BuildCreateWithVersionRequest(Guid organisationId) =>
        new()
        {
            OrganisationId = new OrganisationId(organisationId),
            Name = _fixture.Create<string>(),
            Type = _fixture.Create<string>(),
            Description = _fixture.Create<string>(),
            Provider = _fixture.Create<ResourceTemplateProvider>(),
            Version = _fixture.Create<string>(),
            Source = new ResourceTemplateVersionSource
            {
                BaseUrl = new Uri("https://github.com/test/repo"),
                FolderPath = _fixture.Create<string>(),
                Tag = _fixture.Create<string>()
            },
            Notes = _fixture.Create<string>(),
            State = _fixture.Create<ResourceTemplateVersionState>()
        };

    private UpdateResourceTemplateEndpoint.UpdateResourceTemplateRequest BuildUpdateRequest() =>
        new(_fixture.Create<string>(), _fixture.Create<string>(), _fixture.Create<string>(),
            _fixture.Create<ResourceTemplateProvider>());

    [Fact]
    public async Task ResourceTemplateApi_WhenCreatingResourceTemplate_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var request = BuildCreateRequest(organisation.Id);

        // Act
        var response = await client.PostAsJsonAsync(ResourceTemplatesUrl, request);
        var body = await response.ReadFromJsonAsync<CreateResourceTemplateEndpoint.CreateResourceTemplateResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.IsType<Guid>(body.Id);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenCreatingResourceTemplateWithVersion_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var request = BuildCreateWithVersionRequest(organisation.Id);

        // Act
        var response = await client.PostAsJsonAsync($"{ResourceTemplatesUrl}/with-version", request);
        var body = await response.ReadFromJsonAsync<CreateResourceTemplateWithVersionEndpoint.CreateResourceTemplateWithVersionResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.IsType<Guid>(body.Id);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenGettingResourceTemplateById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createRequest = BuildCreateWithVersionRequest(organisation.Id);
        var createResponse = await client.PostAsJsonAsync($"{ResourceTemplatesUrl}/with-version", createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateResourceTemplateWithVersionEndpoint.CreateResourceTemplateWithVersionResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync($"{ResourceTemplatesUrl}/{created.Id}");
        var body = await response.ReadFromJsonAsync<GetResourceTemplateEndpoint.GetResourceTemplateResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(organisation.Id, body.OrganisationId);
        Assert.Equal(createRequest.Name, body.Name);
        Assert.Equal(createRequest.Type, body.Type);
        Assert.Equal(createRequest.Description, body.Description);
        Assert.Equal(createRequest.Provider, body.Provider);
        Assert.NotEqual(default, body.CreatedAt);
        Assert.NotEqual(default, body.UpdatedAt);
        var version = Assert.Single(body.Versions);
        Assert.Equal(createRequest.Version, version.Version);
        Assert.Equal(createRequest.Source.BaseUrl, version.Source.BaseUrl);
        Assert.Equal(createRequest.Source.FolderPath, version.Source.FolderPath);
        Assert.Equal(createRequest.Source.Tag, version.Source.Tag);
        Assert.Equal(createRequest.Notes, version.Notes);
        Assert.Equal(createRequest.State, version.State);
        Assert.NotEqual(default, version.CreatedAt);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenGettingResourceTemplateByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{ResourceTemplatesUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenGettingAllResourceTemplates_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createRequest = BuildCreateRequest(organisation.Id);
        var createResponse = await client.PostAsJsonAsync(ResourceTemplatesUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateResourceTemplateEndpoint.CreateResourceTemplateResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync(ResourceTemplatesUrl);
        var body = await response.ReadFromJsonAsync<GetAllResourceTemplatesEndpoint.GetAllResourceTemplatesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.ResourceTemplates);

        var template = body.ResourceTemplates.Single(t => t.Id == created.Id);
        Assert.Equal(organisation.Id, template.OrganisationId);
        Assert.Equal(createRequest.Name, template.Name);
        Assert.Equal(createRequest.Type, template.Type);
        Assert.Equal(createRequest.Description, template.Description);
        Assert.Equal(createRequest.Provider, template.Provider);
        Assert.NotEqual(default, template.CreatedAt);
        Assert.NotEqual(default, template.UpdatedAt);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenUpdatingResourceTemplate_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createResponse = await client.PostAsJsonAsync(ResourceTemplatesUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CreateResourceTemplateEndpoint.CreateResourceTemplateResponse>();
        Assert.NotNull(created);

        var updateRequest = BuildUpdateRequest();

        // Act
        var response = await client.PutAsJsonAsync($"{ResourceTemplatesUrl}/{created.Id}", updateRequest);
        var body = await response.ReadFromJsonAsync<UpdateResourceTemplateEndpoint.UpdateResourceTemplateResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(organisation.Id, body.OrganisationId);
        Assert.Equal(updateRequest.Name, body.Name);
        Assert.Equal(updateRequest.Type, body.Type);
        Assert.Equal(updateRequest.Description, body.Description);
        Assert.Equal(updateRequest.Provider, body.Provider);
        Assert.NotEqual(default, body.CreatedAt);
        Assert.NotEqual(default, body.UpdatedAt);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenUpdatingNonExistentResourceTemplate_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.PutAsJsonAsync($"{ResourceTemplatesUrl}/{Guid.NewGuid()}", BuildUpdateRequest());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenDeletingResourceTemplate_ShouldReturn204NoContent()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createResponse = await client.PostAsJsonAsync(ResourceTemplatesUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CreateResourceTemplateEndpoint.CreateResourceTemplateResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.DeleteAsync($"{ResourceTemplatesUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenDeletingNonExistentResourceTemplate_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.DeleteAsync($"{ResourceTemplatesUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResourceTemplateApi_WhenGettingResourceTemplateAfterDeletion_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var createResponse = await client.PostAsJsonAsync(ResourceTemplatesUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CreateResourceTemplateEndpoint.CreateResourceTemplateResponse>();
        Assert.NotNull(created);

        await client.DeleteAsync($"{ResourceTemplatesUrl}/{created.Id}");

        // Act
        var response = await client.GetAsync($"{ResourceTemplatesUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
