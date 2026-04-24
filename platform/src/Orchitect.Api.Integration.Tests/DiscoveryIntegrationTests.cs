using System.Net;
using System.Text.Json;
using AutoFixture;
using Orchitect.Api.Endpoints.Core.Credential;
using Orchitect.Api.Endpoints.Inventory.Discovery;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class DiscoveryIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string DiscoveryUrl = "/discovery";
    private const string CredentialsUrl = "/credentials";
    private readonly Fixture _fixture = new();

    private static readonly JsonElement SamplePayload =
        JsonDocument.Parse("""{"token":"test-token-value"}""").RootElement;

    private CreateCredentialEndpoint.CreateCredentialRequest BuildCredentialRequest(Guid organisationId, CredentialPlatform platform) =>
        new(_fixture.Create<string>(), organisationId, _fixture.Create<CredentialType>(), platform, SamplePayload);

    private CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationRequest BuildDiscoveryRequest(
        Guid organisationId, Guid credentialId, DiscoveryPlatform platform) =>
        new(organisationId.ToString(), credentialId, platform, true, null);

    private async Task<CredentialResponse> CreateCredentialAsync(
        HttpClient client, Guid organisationId, CredentialPlatform platform)
    {
        var response = await client.PostAsJsonAsync(CredentialsUrl, BuildCredentialRequest(organisationId, platform));
        var credential = await response.ReadFromJsonAsync<CredentialResponse>();
        ArgumentNullException.ThrowIfNull(credential);
        return credential;
    }

    [Fact]
    public async Task DiscoveryApi_WhenCreatingDiscoveryConfiguration_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var request = BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub);

        // Act
        var response = await client.PostAsJsonAsync(DiscoveryUrl, request);
        var body = await response.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id.Value);
    }

    [Fact]
    public async Task DiscoveryApi_WhenCreatingDiscoveryConfiguration_WithNonExistentCredential_ShouldReturn400BadRequest()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var request = BuildDiscoveryRequest(organisation.Id, Guid.NewGuid(), DiscoveryPlatform.GitHub);

        // Act
        var response = await client.PostAsJsonAsync(DiscoveryUrl, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenCreatingDiscoveryConfiguration_WithMismatchedPlatform_ShouldReturn400BadRequest()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var request = BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.AzureDevOps);

        // Act
        var response = await client.PostAsJsonAsync(DiscoveryUrl, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenListingDiscoveryConfigurations_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var createRequest = BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub);
        var createResponse = await client.PostAsJsonAsync(DiscoveryUrl, createRequest);
        var created = await createResponse.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<IEnumerable<ListDiscoveryConfigurationsEndpoint.ListDiscoveryConfigurationResponse>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var config = body.Single(c => c.Id.Value == created.Id.Value);
        Assert.Equal(credential.Id, config.CredentialId.Value);
        Assert.Equal(credential.Name, config.CredentialName);
        Assert.Equal(DiscoveryPlatform.GitHub, config.Platform);
        Assert.True(config.IsEnabled);
        Assert.NotEqual(default, config.CreatedAt);
        Assert.NotEqual(default, config.UpdatedAt);
    }

    [Fact]
    public async Task DiscoveryApi_WhenListingDiscoveryConfigurations_ShouldOnlyReturnConfigurationsForOrganisation()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var otherOrganisation = await client.CreateOrganisationAsync();

        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var otherCredential = await CreateCredentialAsync(client, otherOrganisation.Id, CredentialPlatform.GitHub);

        var createResponse = await client.PostAsJsonAsync(DiscoveryUrl, BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub));
        var created = await createResponse.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        Assert.NotNull(created);

        var otherCreateResponse = await client.PostAsJsonAsync(DiscoveryUrl, BuildDiscoveryRequest(otherOrganisation.Id, otherCredential.Id, DiscoveryPlatform.GitHub));
        var otherCreated = await otherCreateResponse.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        Assert.NotNull(otherCreated);

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}?organisationId={organisation.Id}");
        var body = (await response.ReadFromJsonAsync<IEnumerable<ListDiscoveryConfigurationsEndpoint.ListDiscoveryConfigurationResponse>>())?.ToList();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body, c => c.Id.Value == created.Id.Value);
        Assert.DoesNotContain(body, c => c.Id.Value == otherCreated.Id.Value);
    }

    [Fact]
    public async Task DiscoveryApi_WhenUpdatingDiscoveryConfiguration_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var createResponse = await client.PostAsJsonAsync(DiscoveryUrl, BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub));
        var created = await createResponse.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        Assert.NotNull(created);

        var updateRequest = new UpdateDiscoveryConfigurationEndpoint.UpdateDiscoveryConfigurationRequest(organisation.Id.ToString(), false, null);

        // Act
        var response = await client.PutAsJsonAsync($"{DiscoveryUrl}/{created.Id.Value}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenUpdatingNonExistentDiscoveryConfiguration_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var updateRequest = new UpdateDiscoveryConfigurationEndpoint.UpdateDiscoveryConfigurationRequest(organisation.Id.ToString(), false, null);

        // Act
        var response = await client.PutAsJsonAsync($"{DiscoveryUrl}/{Guid.NewGuid()}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenDeletingDiscoveryConfiguration_ShouldReturn204NoContent()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var createResponse = await client.PostAsJsonAsync(DiscoveryUrl, BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub));
        var created = await createResponse.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.DeleteAsync($"{DiscoveryUrl}/{created.Id.Value}?organisationId={organisation.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenDeletingNonExistentDiscoveryConfiguration_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();

        // Act
        var response = await client.DeleteAsync($"{DiscoveryUrl}/{Guid.NewGuid()}?organisationId={organisation.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenListingDiscoveryConfigurations_AfterDeletion_ShouldNotContainDeletedConfig()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var createResponse = await client.PostAsJsonAsync(DiscoveryUrl, BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub));
        var created = await createResponse.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        Assert.NotNull(created);

        await client.DeleteAsync($"{DiscoveryUrl}/{created.Id.Value}?organisationId={organisation.Id}");

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<IEnumerable<ListDiscoveryConfigurationsEndpoint.ListDiscoveryConfigurationResponse>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.DoesNotContain(body, c => c.Id.Value == created.Id.Value);
    }

    [Fact]
    public async Task DiscoveryApi_WhenTriggeringDiscovery_ShouldReturn202Accepted()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var createResponse = await client.PostAsJsonAsync(DiscoveryUrl, BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub));
        var created = await createResponse.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        Assert.NotNull(created);

        // Act
        var response = await client.PostAsync($"{DiscoveryUrl}/{created.Id.Value}/trigger?organisationId={organisation.Id}", null);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenTriggeringNonExistentDiscovery_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();

        // Act
        var response = await client.PostAsync($"{DiscoveryUrl}/{Guid.NewGuid()}/trigger?organisationId={organisation.Id}", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
