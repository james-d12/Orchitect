using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Inventory.Issue;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class IssueIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string IssuesUrl = "/issues";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task IssueApi_WhenGettingIssueById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedIssueAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{IssuesUrl}/{seeded.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetIssueEndpoint.GetIssueResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(seeded.Id.Value, body.Id);
        Assert.Equal(seeded.Title, body.Title);
        Assert.Equal(seeded.Description, body.Description);
        Assert.Equal(seeded.Url, body.Url);
        Assert.Equal(seeded.Type, body.Type);
        Assert.Equal(seeded.State, body.State);
        Assert.Equal(seeded.Platform, body.Platform);
    }

    [Fact]
    public async Task IssueApi_WhenGettingIssueByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{IssuesUrl}/{_fixture.Create<string>()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IssueApi_WhenGettingAllIssues_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedIssueAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{IssuesUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllIssuesEndpoint.GetAllIssuesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Issues);
    }

    [Fact]
    public async Task IssueApi_WhenGettingAllIssues_ShouldNotReturnResourcesFromOtherOrganisations()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var otherOrganisation = await client.CreateOrganisationAsync();

        var seeded = await factory.SeedIssueAsync(new OrganisationId(organisation.Id));
        await factory.SeedIssueAsync(new OrganisationId(otherOrganisation.Id));

        // Act
        var response = await client.GetAsync($"{IssuesUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllIssuesEndpoint.GetAllIssuesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Issues);
        Assert.Contains(body.Issues, i => i.Id == seeded.Id.Value);
    }

    [Fact]
    public async Task IssueApi_WhenGettingAllIssues_ShouldFilterByTitle()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedIssueAsync(new OrganisationId(organisation.Id));
        await factory.SeedIssueAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{IssuesUrl}?organisationId={organisation.Id}&title={seeded.Title}");
        var body = await response.ReadFromJsonAsync<GetAllIssuesEndpoint.GetAllIssuesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Issues);
        Assert.Equal(seeded.Title, body.Issues[0].Title);
    }

}
