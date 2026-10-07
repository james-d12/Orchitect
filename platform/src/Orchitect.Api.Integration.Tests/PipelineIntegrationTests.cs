using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Inventory.Pipeline;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Pipeline;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class PipelineIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string PipelinesUrl = "/pipelines";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task PipelineApi_WhenGettingPipelineById_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedPipelineAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PipelinesUrl}/{seeded.Id.Value}");
        var body = await response.ReadFromJsonAsync<GetPipelineEndpoint.GetPipelineResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(seeded.Id.Value, body.Id);
        Assert.Equal(seeded.Name, body.Name);
        Assert.Equal(seeded.Url, body.Url);
        Assert.Equal(seeded.Platform, body.Platform);
        Assert.Equal(seeded.User.Name, body.OwnerName);
    }

    [Fact]
    public async Task PipelineApi_WhenGettingPipelineByNonExistentId_ShouldReturn404NotFound()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();

        // Act
        var response = await client.GetAsync($"{PipelinesUrl}/{_fixture.Create<string>()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PipelineApi_WhenGettingAllPipelines_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        await factory.SeedPipelineAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PipelinesUrl}?organisationId={organisation.Id}");
        var body = await response.ReadFromJsonAsync<GetAllPipelinesEndpoint.GetAllPipelinesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Pipelines);
    }

    [Fact]
    public async Task PipelineApi_WhenGettingAllPipelines_ShouldFilterByName()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedPipelineAsync(new OrganisationId(organisation.Id));
        await factory.SeedPipelineAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PipelinesUrl}?organisationId={organisation.Id}&name={seeded.Name}");
        var body = await response.ReadFromJsonAsync<GetAllPipelinesEndpoint.GetAllPipelinesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Pipelines);
        Assert.Equal(seeded.Id.Value, body.Pipelines[0].Id);
        Assert.Equal(seeded.User.Name, body.Pipelines[0].OwnerName);
    }

    [Fact]
    public async Task PipelineApi_WhenGettingAllPipelines_ShouldFilterByUrl()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedPipelineAsync(new OrganisationId(organisation.Id));
        await factory.SeedPipelineAsync(new OrganisationId(organisation.Id));

        // Act
        var response = await client.GetAsync($"{PipelinesUrl}?organisationId={organisation.Id}&url={seeded.Url}");
        var body = await response.ReadFromJsonAsync<GetAllPipelinesEndpoint.GetAllPipelinesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Pipelines);
        Assert.Equal(seeded.Id.Value, body.Pipelines[0].Id);
        Assert.Equal(seeded.Url, body.Pipelines[0].Url);
    }

    [Fact]
    public async Task PipelineApi_WhenGettingAllPipelines_ShouldFilterByPlatform()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var seeded = await factory.SeedPipelineAsync(new OrganisationId(organisation.Id), platform: PipelinePlatform.AzureDevOps);
        await factory.SeedPipelineAsync(new OrganisationId(organisation.Id), platform: PipelinePlatform.GitHub);

        // Act
        var response = await client.GetAsync($"{PipelinesUrl}?organisationId={organisation.Id}&platform={seeded.Platform}");
        var body = await response.ReadFromJsonAsync<GetAllPipelinesEndpoint.GetAllPipelinesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Pipelines);
        Assert.Equal(seeded.Id.Value, body.Pipelines[0].Id);
        Assert.Equal(seeded.Platform, body.Pipelines[0].Platform);
    }

    [Fact]
    public async Task PipelineApi_WhenPipelinesShareAnExistingOwner_ShouldReuseOwner()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var organisation = await client.CreateOrganisationAsync();
        var organisationId = new OrganisationId(organisation.Id);
        var owner = await factory.SeedUserAsync(organisationId);
        var first = await factory.SeedPipelineAsync(organisationId, owner);
        var second = await factory.SeedPipelineAsync(organisationId, owner);

        // Act
        var response = await client.GetAsync($"{PipelinesUrl}?organisationId={organisation.Id}&ownerName={owner.Name}");
        var body = await response.ReadFromJsonAsync<GetAllPipelinesEndpoint.GetAllPipelinesResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Pipelines.Count);
        Assert.Contains(body.Pipelines, p => p.Id == first.Id.Value);
        Assert.Contains(body.Pipelines, p => p.Id == second.Id.Value);
        Assert.All(body.Pipelines, p => Assert.Equal(owner.Name, p.OwnerName));
    }
}
