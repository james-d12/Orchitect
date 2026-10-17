using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Inventory.SourceControl;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.SourceControl;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class PullRequestIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string PullRequestsUrl = "/pull-requests";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task PullRequestApi_WhenGettingPullRequestById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PullRequestsUrl}/{seeded.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetPullRequestEndpoint.GetPullRequestResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(seeded.Id.Value, body.Id);
        Assert.Equal(seeded.Name, body.Name);
        Assert.Equal(seeded.Description, body.Description);
        Assert.Equal(seeded.Url, body.Url);
        Assert.Equal(seeded.Status, body.Status);
        Assert.Equal(seeded.Platform, body.Platform);
        Assert.Equal(seeded.RepositoryUrl, body.RepositoryUrl);
        Assert.Equal(seeded.RepositoryName, body.RepositoryName);
        Assert.Equal(seeded.CreatedOnDate, body.CreatedOnDate);
    }

    [Fact]
    public async Task PullRequestApi_WhenGettingPullRequestByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{PullRequestsUrl}/{_fixture.Create<string>()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PullRequestApi_WhenGettingAllPullRequests_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PullRequestsUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllPullRequestsEndpoint.GetAllPullRequestsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.PullRequests);
    }

    [Fact]
    public async Task PullRequestApi_WhenGettingAllPullRequests_ShouldFilterByName()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id));
        await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PullRequestsUrl}?organisationId={organisation.Id}&name={seeded.Name}");
        var body = await response.ReadFromJsonAsync<GetAllPullRequestsEndpoint.GetAllPullRequestsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.PullRequests);
        Assert.Equal(seeded.Name, body.PullRequests[0].Name);
    }

    [Fact]
    public async Task PullRequestApi_WhenGettingAllPullRequests_ShouldFilterByPlatform()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id), platform: PullRequestPlatform.GitHub);
        await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id), platform: PullRequestPlatform.AzureDevOps);

        // Act
        var response = await client.GetAsync($"{PullRequestsUrl}?organisationId={organisation.Id}&platform=GitHub");
        var body = await response.ReadFromJsonAsync<GetAllPullRequestsEndpoint.GetAllPullRequestsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.PullRequests);
        Assert.All(body.PullRequests, pr => Assert.Equal(PullRequestPlatform.GitHub, pr.Platform));
    }

    [Fact]
    public async Task PullRequestApi_WhenGettingAllPullRequests_ShouldFilterByLabel()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var label = Guid.NewGuid().ToString();
        var seeded = await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id), labels: [label]);
        await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PullRequestsUrl}?organisationId={organisation.Id}&labels={label}");
        var body = await response.ReadFromJsonAsync<GetAllPullRequestsEndpoint.GetAllPullRequestsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.PullRequests);
        Assert.Equal(seeded.Id.Value, body.PullRequests[0].Id);
    }

    [Fact]
    public async Task PullRequestApi_WhenGettingAllPullRequests_ShouldFilterByUrl()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id));
        await factory.SeedPullRequestAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PullRequestsUrl}?organisationId={organisation.Id}&url={Uri.EscapeDataString(seeded.Url.ToString())}");
        var body = await response.ReadFromJsonAsync<GetAllPullRequestsEndpoint.GetAllPullRequestsResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.PullRequests);
        Assert.Equal(seeded.Id.Value, body.PullRequests[0].Id);
    }
}
