using System.Net;
using AutoFixture;
using Orchitect.Api.Endpoints.Core.Organisation;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class OrganisationIntegrationTests(WebApplicationFactoryWithPostgres factory)
{
    private const string OrganisationsUrl = "/organisations";
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task OrganisationApi_WhenCreatingOrganisation_ShouldReturn200Ok()
    {
        // Arrange
        var client = await factory.CreateClient().AddAuthorisationHeader();
        var request = _fixture.Create<CreateOrganisationRequest>();

        // Act
        var response = await client.PostAsJsonAsync(OrganisationsUrl, request);
        var body = await response.ReadFromJsonAsync<CreateOrganisationEndpoint.CreateOrganisationResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.IsType<Guid>(body.Id);
        Assert.Equal(request.Name, body.Name);
    }
}