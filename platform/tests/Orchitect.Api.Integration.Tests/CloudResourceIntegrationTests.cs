using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Inventory.Cloud;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class CloudResourceIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string CloudResourcesUrl = "/cloud/resources";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task CloudResourceApi_WhenGettingCloudResourceById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedCloudResourceAsync(new OrganisationId(organisation.Id));


        // Act
        var response = await client.GetAsync($"{CloudResourcesUrl}/{seeded.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetCloudResourceEndpoint.GetCloudResourceResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(seeded.Id.Value, body.Id);
        Assert.Equal(seeded.Name, body.Name);
        Assert.Equal(seeded.Description, body.Description);
        Assert.Equal(seeded.Platform, body.Platform);
        Assert.Equal(seeded.Type, body.Type);
        Assert.Equal(seeded.Url, body.Url);
    }

    [Fact]
    public async Task CloudResourceApi_WhenGettingCloudResourceByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{CloudResourcesUrl}/{_fixture.Create<string>()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CloudResourceApi_WhenGettingAllCloudResources_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedCloudResourceAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{CloudResourcesUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllCloudResourcesEndpoint.GetAllCloudResourcesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.CloudResources);
    }

    [Fact]
    public async Task CloudResourceApi_WhenGettingAllCloudResources_ShouldFilterByName()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedCloudResourceAsync(new OrganisationId(organisation.Id));
        await factory.SeedCloudResourceAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{CloudResourcesUrl}?organisationId={organisation.Id}&name={seeded.Name}");
        var body = await response.ReadFromJsonAsync<GetAllCloudResourcesEndpoint.GetAllCloudResourcesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.CloudResources);
        Assert.Equal(seeded.Name, body.CloudResources[0].Name);
    }

    [Fact]
    public async Task CloudResourceApi_WhenGettingAllCloudResources_ShouldFilterByPlatform()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedCloudResourceAsync(new OrganisationId(organisation.Id), CloudPlatform.Azure);
        await factory.SeedCloudResourceAsync(new OrganisationId(organisation.Id), CloudPlatform.Aws);

        // Act
        var response = await client.GetAsync($"{CloudResourcesUrl}?organisationId={organisation.Id}&platform=Azure");
        var body = await response.ReadFromJsonAsync<GetAllCloudResourcesEndpoint.GetAllCloudResourcesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.CloudResources);
        Assert.All(body.CloudResources, r => Assert.Equal(CloudPlatform.Azure, r.Platform));
    }

    [Fact]
    public async Task CloudResourceApi_WhenGettingAllCloudResources_ShouldNotReturnResourcesFromOtherOrganisations()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var otherOrganisation = await client.CreateOrganisationAsync();

        var seeded = await factory.SeedCloudResourceAsync(new OrganisationId(organisation.Id));
        await factory.SeedCloudResourceAsync(new OrganisationId(otherOrganisation.Id));

        // Act
        var response = await client.GetAsync($"{CloudResourcesUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllCloudResourcesEndpoint.GetAllCloudResourcesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.CloudResources);
        Assert.Contains(body.CloudResources, r => r.Id == seeded.Id.Value);
        Assert.DoesNotContain(body.CloudResources, r => r.Id != seeded.Id.Value);
    }
}
