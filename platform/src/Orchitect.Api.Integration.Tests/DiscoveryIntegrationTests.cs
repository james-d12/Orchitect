using System.Net;
using System.Text.Json;
using AutoFixture;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Api.Endpoints.Core.Credential;
using Orchitect.Api.Endpoints.Inventory.Discovery;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

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
        Guid organisationId, Guid credentialId, DiscoveryPlatform platform, string? name = null) =>
        new(organisationId.ToString(), credentialId, name ?? _fixture.Create<string>(), platform, true, null);

    private async Task<CredentialResponse> CreateCredentialAsync(
        HttpClient client, Guid organisationId, CredentialPlatform platform)
    {
        var response = await client.PostAsJsonAsync(CredentialsUrl, BuildCredentialRequest(organisationId, platform));
        var credential = await response.ReadFromJsonAsync<CredentialResponse>();
        ArgumentNullException.ThrowIfNull(credential);
        return credential;
    }

    private async Task<Guid> CreateDisabledDiscoveryConfigurationAsync(HttpClient client, Guid organisationId)
    {
        var credential = await CreateCredentialAsync(client, organisationId, CredentialPlatform.GitHub);
        var request = BuildDiscoveryRequest(organisationId, credential.Id, DiscoveryPlatform.GitHub) with
        {
            IsEnabled = false
        };
        var response = await client.PostAsJsonAsync(DiscoveryUrl, request);
        var created = await response.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        ArgumentNullException.ThrowIfNull(created);
        return created.Id.Value;
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DiscoveryApi_WhenCreatingDiscoveryConfiguration_WithBlankName_ShouldReturn400BadRequest(string name)
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var request = BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub, name);

        // Act
        var response = await client.PostAsJsonAsync(DiscoveryUrl, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenCreatingDiscoveryConfiguration_WithNameTooLong_ShouldReturn400BadRequest()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var request = BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub,
            new string('a', DiscoveryConfiguration.NameMaxLength + 1));

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
        Assert.Equal(createRequest.Name, config.Name);
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

        var updateRequest = new UpdateDiscoveryConfigurationEndpoint.UpdateDiscoveryConfigurationRequest(organisation.Id.ToString(), "Renamed", false, null);

        // Act
        var response = await client.PutAsJsonAsync($"{DiscoveryUrl}/{created.Id.Value}", updateRequest);
        var listResponse = await client.GetAsync($"{DiscoveryUrl}?organisationId={organisation.Id}");
        var configs = await listResponse.ReadFromJsonAsync<IEnumerable<ListDiscoveryConfigurationsEndpoint.ListDiscoveryConfigurationResponse>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(configs);
        var config = configs.Single(c => c.Id.Value == created.Id.Value);
        Assert.Equal("Renamed", config.Name);
        Assert.False(config.IsEnabled);
    }

    [Fact]
    public async Task DiscoveryApi_WhenUpdatingDiscoveryConfiguration_WithBlankName_ShouldReturn400BadRequest()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var credential = await CreateCredentialAsync(client, organisation.Id, CredentialPlatform.GitHub);
        var createResponse = await client.PostAsJsonAsync(DiscoveryUrl, BuildDiscoveryRequest(organisation.Id, credential.Id, DiscoveryPlatform.GitHub));
        var created = await createResponse.ReadFromJsonAsync<CreateDiscoveryConfigurationEndpoint.CreateDiscoveryConfigurationResponse>();
        Assert.NotNull(created);

        var updateRequest = new UpdateDiscoveryConfigurationEndpoint.UpdateDiscoveryConfigurationRequest(organisation.Id.ToString(), " ", false, null);

        // Act
        var response = await client.PutAsJsonAsync($"{DiscoveryUrl}/{created.Id.Value}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenUpdatingNonExistentDiscoveryConfiguration_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var updateRequest = new UpdateDiscoveryConfigurationEndpoint.UpdateDiscoveryConfigurationRequest(organisation.Id.ToString(), "Renamed", false, null);

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

    [Fact]
    public async Task DiscoveryApi_WhenGettingDiscoveryConfiguration_ShouldReturn200OkWithLatestRun()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var configId = await CreateDisabledDiscoveryConfigurationAsync(client, organisation.Id);
        var counts = new DiscoveryCounts { Repositories = 3, Pipelines = 2 };
        var run = await factory.SeedDiscoveryRunAsync(new DiscoveryConfigurationId(configId), DateTime.UtcNow,
            r => r.Succeed(counts));

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}/{configId}");
        var body = await response.ReadFromJsonAsync<GetDiscoveryConfigurationEndpoint.GetDiscoveryConfigurationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(configId, body.Id);
        Assert.Equal(organisation.Id, body.OrganisationId);
        Assert.Equal(DiscoveryPlatform.GitHub, body.Platform);
        Assert.False(body.IsEnabled);
        Assert.NotNull(body.LatestRun);
        Assert.Equal(run.Id.Value, body.LatestRun.Id);
        Assert.Equal(DiscoveryRunStatus.Succeeded, body.LatestRun.Status);
        Assert.Equal(counts, body.LatestRun.Counts);
        Assert.Equal(5, body.LatestRun.Counts.Total);
    }

    [Fact]
    public async Task DiscoveryApi_WhenGettingDiscoveryConfiguration_WithNoRuns_ShouldReturnNoLatestRun()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var configId = await CreateDisabledDiscoveryConfigurationAsync(client, organisation.Id);

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}/{configId}");
        var body = await response.ReadFromJsonAsync<GetDiscoveryConfigurationEndpoint.GetDiscoveryConfigurationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Null(body.LatestRun);
    }

    [Fact]
    public async Task DiscoveryApi_WhenGettingNonExistentDiscoveryConfiguration_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenNotAMember_ShouldReturn404NotFoundForConfigurationAndRuns()
    {
        // Arrange
        var member = await factory.CreateClient().AddAuthorisationHeader();
        var outsider = await factory.CreateClient().AddAuthorisationHeaderForNewUser();
        var organisation = await member.CreateOrganisationAsync();
        var configId = await CreateDisabledDiscoveryConfigurationAsync(member, organisation.Id);
        await factory.SeedDiscoveryRunAsync(new DiscoveryConfigurationId(configId), DateTime.UtcNow);

        // Act
        var get = await outsider.GetAsync($"{DiscoveryUrl}/{configId}");
        var status = await outsider.GetAsync($"{DiscoveryUrl}/{configId}/status");
        var runs = await outsider.GetAsync($"{DiscoveryUrl}/{configId}/runs");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, runs.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenGettingStatus_WithNoRuns_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var configId = await CreateDisabledDiscoveryConfigurationAsync(client, organisation.Id);

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}/{configId}/status");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenGettingStatus_ShouldReturnLatestRun()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var configId = new DiscoveryConfigurationId(await CreateDisabledDiscoveryConfigurationAsync(client, organisation.Id));
        var now = DateTime.UtcNow;
        await factory.SeedDiscoveryRunAsync(configId, now.AddMinutes(-30), r => r.Succeed(new DiscoveryCounts()));
        var latest = await factory.SeedDiscoveryRunAsync(configId, now.AddMinutes(-1), r => r.Fail("Bad credentials"));

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}/{configId.Value}/status");
        var body = await response.ReadFromJsonAsync<DiscoveryRunResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(latest.Id.Value, body.Id);
        Assert.Equal(configId.Value, body.DiscoveryConfigurationId);
        Assert.Equal(DiscoveryRunStatus.Failed, body.Status);
        Assert.Equal("Bad credentials", body.ErrorMessage);
        Assert.NotNull(body.CompletedAt);
    }

    [Fact]
    public async Task DiscoveryApi_WhenListingRuns_ShouldReturnNewestFirstUpToLimit()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var configId = new DiscoveryConfigurationId(await CreateDisabledDiscoveryConfigurationAsync(client, organisation.Id));
        var now = DateTime.UtcNow;
        var oldest = await factory.SeedDiscoveryRunAsync(configId, now.AddMinutes(-3));
        var middle = await factory.SeedDiscoveryRunAsync(configId, now.AddMinutes(-2));
        var newest = await factory.SeedDiscoveryRunAsync(configId, now.AddMinutes(-1));

        // Act
        var all = await client.GetAsync($"{DiscoveryUrl}/{configId.Value}/runs");
        var allBody = await all.ReadFromJsonAsync<List<DiscoveryRunResponse>>();
        var limited = await client.GetAsync($"{DiscoveryUrl}/{configId.Value}/runs?limit=2");
        var limitedBody = await limited.ReadFromJsonAsync<List<DiscoveryRunResponse>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.NotNull(allBody);
        Assert.Equal([newest.Id.Value, middle.Id.Value, oldest.Id.Value], allBody.Select(r => r.Id));
        Assert.Equal(HttpStatusCode.OK, limited.StatusCode);
        Assert.NotNull(limitedBody);
        Assert.Equal([newest.Id.Value, middle.Id.Value], limitedBody.Select(r => r.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task DiscoveryApi_WhenListingRuns_WithInvalidLimit_ShouldReturn400BadRequest(int limit)
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var configId = await CreateDisabledDiscoveryConfigurationAsync(client, organisation.Id);

        // Act
        var response = await client.GetAsync($"{DiscoveryUrl}/{configId}/runs?limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DiscoveryApi_WhenTriggeringDiscovery_ShouldRecordRun()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var configId = await CreateDisabledDiscoveryConfigurationAsync(client, organisation.Id);

        // Act
        var response = await client.PostAsync($"{DiscoveryUrl}/{configId}/trigger?organisationId={organisation.Id}", null);
        var body = await response.ReadFromJsonAsync<TriggerDiscoveryEndpoint.TriggerDiscoveryResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal($"/discovery/{configId}/status", response.Headers.Location?.OriginalString);

        using var scope = factory.Services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<IDiscoveryRunRepository>()
            .GetByIdAsync(new DiscoveryRunId(body.RunId));
        Assert.NotNull(run);
        Assert.Equal(configId, run.DiscoveryConfigurationId.Value);
    }

    [Fact]
    public async Task DiscoveryApi_WhenDeletingDiscoveryConfiguration_ShouldDeleteItsRuns()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var configId = await CreateDisabledDiscoveryConfigurationAsync(client, organisation.Id);
        var run = await factory.SeedDiscoveryRunAsync(new DiscoveryConfigurationId(configId), DateTime.UtcNow);

        // Act
        var response = await client.DeleteAsync($"{DiscoveryUrl}/{configId}?organisationId={organisation.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var deleted = await scope.ServiceProvider.GetRequiredService<IDiscoveryRunRepository>().GetByIdAsync(run.Id);
        Assert.Null(deleted);
    }
}
