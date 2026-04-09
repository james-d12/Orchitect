using System.Net;
using System.Text.Json;
using AutoFixture;
using Orchitect.Api.Endpoints.Core.Credential;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Credential;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class CredentialIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string CredentialsUrl = "/credentials";
    private readonly Fixture _fixture = new();

    private static readonly JsonElement SamplePayload =
        JsonDocument.Parse("""{"token":"test-token-value"}""").RootElement;

    private CreateCredentialRequest BuildCreateRequest(Guid organisationId) =>
        new(_fixture.Create<string>(), organisationId, _fixture.Create<CredentialType>(),
            _fixture.Create<CredentialPlatform>(), SamplePayload);

    private UpdateCredentialEndpoint.UpdateCredentialRequest BuildUpdateRequest() =>
        new(_fixture.Create<string>(), _fixture.Create<CredentialType>(),
            _fixture.Create<CredentialPlatform>(), SamplePayload);

    [Fact]
    public async Task CredentialApi_WhenCreatingCredential_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var request = BuildCreateRequest(organisation.Id);

        // Act
        var response = await client.PostAsJsonAsync(CredentialsUrl, request);
        var body = await response.ReadFromJsonAsync<CredentialResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.IsType<Guid>(body.Id);
        Assert.Equal(request.Name, body.Name);
        Assert.Equal(organisation.Id, body.OrganisationId);
        Assert.Equal(request.Type, body.Type);
        Assert.Equal(request.Platform, body.Platform);
    }

    [Fact]
    public async Task CredentialApi_WhenGettingCredentialById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();

        var createResponse = await client.PostAsJsonAsync(CredentialsUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CredentialResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync($"{CredentialsUrl}/{created.Id}");
        var body = await response.ReadFromJsonAsync<CredentialResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(created.Name, body.Name);
        Assert.Equal(created.OrganisationId, body.OrganisationId);
    }

    [Fact]
    public async Task CredentialApi_WhenGettingCredentialByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{CredentialsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CredentialApi_WhenGettingAllCredentials_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await client.PostAsJsonAsync(CredentialsUrl, BuildCreateRequest(organisation.Id));

        // Act
        var response = await client.GetAsync($"{CredentialsUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllCredentialsEndpoint.GetAllCredentialsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Credentials);
        Assert.All(body.Credentials, c => Assert.Equal(organisation.Id, c.OrganisationId));
    }

    [Fact]
    public async Task CredentialApi_WhenGettingAllCredentials_ShouldOnlyReturnCredentialsForOrganisation()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var otherOrganisation = await client.CreateOrganisationAsync();

        await client.PostAsJsonAsync(CredentialsUrl, BuildCreateRequest(organisation.Id));
        await client.PostAsJsonAsync(CredentialsUrl, BuildCreateRequest(otherOrganisation.Id));

        // Act
        var response = await client.GetAsync($"{CredentialsUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllCredentialsEndpoint.GetAllCredentialsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.All(body.Credentials, c => Assert.Equal(organisation.Id, c.OrganisationId));
    }

    [Fact]
    public async Task CredentialApi_WhenUpdatingCredential_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();

        var createResponse = await client.PostAsJsonAsync(CredentialsUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CredentialResponse>();
        Assert.NotNull(created);

        var updateRequest = BuildUpdateRequest();

        // Act
        var response = await client.PutAsJsonAsync($"{CredentialsUrl}/{created.Id}", updateRequest);
        var body = await response.ReadFromJsonAsync<CredentialResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(updateRequest.Name, body.Name);
        Assert.Equal(updateRequest.Type, body.Type);
        Assert.Equal(updateRequest.Platform, body.Platform);
    }

    [Fact]
    public async Task CredentialApi_WhenUpdatingNonExistentCredential_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.PutAsJsonAsync($"{CredentialsUrl}/{Guid.NewGuid()}", BuildUpdateRequest());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CredentialApi_WhenDeletingCredential_ShouldReturn204NoContent()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();

        var createResponse = await client.PostAsJsonAsync(CredentialsUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CredentialResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.DeleteAsync($"{CredentialsUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CredentialApi_WhenDeletingNonExistentCredential_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.DeleteAsync($"{CredentialsUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CredentialApi_WhenGettingCredentialAfterDeletion_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();

        var createResponse = await client.PostAsJsonAsync(CredentialsUrl, BuildCreateRequest(organisation.Id));
        var created = await createResponse.ReadFromJsonAsync<CredentialResponse>();
        Assert.NotNull(created);

        await client.DeleteAsync($"{CredentialsUrl}/{created.Id}");

        // Act
        var response = await client.GetAsync($"{CredentialsUrl}/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}