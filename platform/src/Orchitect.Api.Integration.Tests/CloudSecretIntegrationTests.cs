using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Inventory.Cloud;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class CloudSecretIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string CloudSecretsUrl = "/cloud/secrets";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task CloudSecretApi_WhenGettingCloudSecretById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedCloudSecretAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{CloudSecretsUrl}/{seeded.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetCloudSecretEndpoint.GetCloudSecretResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(seeded.Id.Value, body.Id);
        Assert.Equal(seeded.Name, body.Name);
        Assert.Equal(seeded.Location, body.Location);
        Assert.Equal(seeded.Platform, body.Platform);
        Assert.Equal(seeded.Url, body.Url);
    }

    [Fact]
    public async Task CloudSecretApi_WhenGettingCloudSecretByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{CloudSecretsUrl}/{_fixture.Create<string>()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CloudSecretApi_WhenGettingAllCloudSecrets_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedCloudSecretAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{CloudSecretsUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllCloudSecretsEndpoint.GetAllCloudSecretsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.CloudSecrets);
    }

    [Fact]
    public async Task CloudSecretApi_WhenGettingAllCloudSecrets_ShouldFilterByName()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedCloudSecretAsync(new OrganisationId(organisation.Id));
        await factory.SeedCloudSecretAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{CloudSecretsUrl}?organisationId={organisation.Id}&name={seeded.Name}");
        var body = await response.ReadFromJsonAsync<GetAllCloudSecretsEndpoint.GetAllCloudSecretsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.CloudSecrets);
        Assert.Equal(seeded.Name, body.CloudSecrets[0].Name);
    }

    [Fact]
    public async Task CloudSecretApi_WhenGettingAllCloudSecrets_ShouldFilterByLocation()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedCloudSecretAsync(new OrganisationId(organisation.Id));
        await factory.SeedCloudSecretAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{CloudSecretsUrl}?organisationId={organisation.Id}&location={seeded.Location}");
        var body = await response.ReadFromJsonAsync<GetAllCloudSecretsEndpoint.GetAllCloudSecretsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.CloudSecrets);
        Assert.Equal(seeded.Location, body.CloudSecrets[0].Location);
    }

    [Fact]
    public async Task CloudSecretApi_WhenGettingAllCloudSecrets_ShouldFilterByPlatform()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedCloudSecretAsync(new OrganisationId(organisation.Id), CloudSecretPlatform.Azure);
        await factory.SeedCloudSecretAsync(new OrganisationId(organisation.Id), CloudSecretPlatform.Aws);

        // Act
        var response = await client.GetAsync($"{CloudSecretsUrl}?organisationId={organisation.Id}&platform=Azure");
        var body = await response.ReadFromJsonAsync<GetAllCloudSecretsEndpoint.GetAllCloudSecretsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.CloudSecrets);
        Assert.All(body.CloudSecrets, s => Assert.Equal(CloudSecretPlatform.Azure, s.Platform));
    }
}
