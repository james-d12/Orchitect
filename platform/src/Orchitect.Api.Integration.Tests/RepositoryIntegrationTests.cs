using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Inventory.SourceControl;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.SourceControl;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class RepositoryIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string RepositoriesUrl = "/repositories";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task RepositoryApi_WhenGettingRepositoryById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{RepositoriesUrl}/{seeded.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetRepositoryEndpoint.GetRepositoryResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(seeded.Id.Value, body.Id);
        Assert.Equal(seeded.Name, body.Name);
        Assert.Equal(seeded.DefaultBranch, body.DefaultBranch);
        Assert.Equal(seeded.Url, body.Url);
        Assert.Equal(seeded.Platform, body.Platform);
        Assert.Equal(seeded.User.Name, body.OwnerName);
    }

    [Fact]
    public async Task RepositoryApi_WhenGettingRepositoryByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{RepositoriesUrl}/{_fixture.Create<string>()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RepositoryApi_WhenGettingAllRepositories_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{RepositoriesUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllRepositoriesEndpoint.GetAllRepositoriesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Repositories);
    }

    [Fact]
    public async Task RepositoryApi_WhenGettingAllRepositories_ShouldFilterByName()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id));
        await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{RepositoriesUrl}?organisationId={organisation.Id}&name={seeded.Name}");
        var body = await response.ReadFromJsonAsync<GetAllRepositoriesEndpoint.GetAllRepositoriesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Repositories);
        Assert.Equal(seeded.Name, body.Repositories[0].Name);
        Assert.Equal(seeded.User.Name, body.Repositories[0].OwnerName);
        Assert.Equal(seeded.Url, body.Repositories[0].Url);
        Assert.Equal(seeded.DefaultBranch, body.Repositories[0].DefaultBranch);
        Assert.Equal(seeded.Platform, body.Repositories[0].Platform);
    }
    
    [Fact]
    public async Task RepositoryApi_WhenGettingAllRepositories_ShouldFilterByUrl()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id));
        await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{RepositoriesUrl}?organisationId={organisation.Id}&url={seeded.Url}");
        var body = await response.ReadFromJsonAsync<GetAllRepositoriesEndpoint.GetAllRepositoriesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Repositories);
        Assert.Equal(seeded.Name, body.Repositories[0].Name);
        Assert.Equal(seeded.User.Name, body.Repositories[0].OwnerName);
        Assert.Equal(seeded.Url, body.Repositories[0].Url);
        Assert.Equal(seeded.DefaultBranch, body.Repositories[0].DefaultBranch);
        Assert.Equal(seeded.Platform, body.Repositories[0].Platform);
    }
    
    [Fact]
    public async Task RepositoryApi_WhenGettingAllRepositories_ShouldFilterByDefaultBranch()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id));
        await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{RepositoriesUrl}?organisationId={organisation.Id}&defaultBranch={seeded.DefaultBranch}");
        var body = await response.ReadFromJsonAsync<GetAllRepositoriesEndpoint.GetAllRepositoriesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Repositories);
        Assert.Equal(seeded.Name, body.Repositories[0].Name);
        Assert.Equal(seeded.User.Name, body.Repositories[0].OwnerName);
        Assert.Equal(seeded.Url, body.Repositories[0].Url);
        Assert.Equal(seeded.DefaultBranch, body.Repositories[0].DefaultBranch);
        Assert.Equal(seeded.Platform, body.Repositories[0].Platform);
    }
    
    [Fact]
    public async Task RepositoryApi_WhenGettingAllRepositories_ShouldFilterByPlatform()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id), platform: RepositoryPlatform.AzureDevOps);
        await factory.SeedRepositoryAsync(new OrganisationId(organisation.Id), platform: RepositoryPlatform.GitHub);

        // Act
        var response = await client.GetAsync($"{RepositoriesUrl}?organisationId={organisation.Id}&platform={seeded.Platform}");
        var body = await response.ReadFromJsonAsync<GetAllRepositoriesEndpoint.GetAllRepositoriesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Repositories);
        Assert.Equal(seeded.Name, body.Repositories[0].Name);
        Assert.Equal(seeded.User.Name, body.Repositories[0].OwnerName);
        Assert.Equal(seeded.Url, body.Repositories[0].Url);
        Assert.Equal(seeded.DefaultBranch, body.Repositories[0].DefaultBranch);
        Assert.Equal(seeded.Platform, body.Repositories[0].Platform);
    }
}