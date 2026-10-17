using AutoFixture;
using Orchitect.Api.Endpoints.Core.Organisation;

namespace Orchitect.Api.Integration.Tests.Helpers;

public static class OrganisationSetupHelper
{
    private static readonly Fixture Fixture = new();
    private const string OrganisationsUrl = "/organisations";

    public static async Task<CreateOrganisationEndpoint.CreateOrganisationResponse> CreateOrganisationAsync(
        this HttpClient client)
    {
        var organisationName = Fixture.Create<string>();
        var request = new CreateOrganisationEndpoint.CreateOrganisationRequest(organisationName);
        var createOrgResponse = await client.PostAsJsonAsync(OrganisationsUrl, request);
        var org = await createOrgResponse.ReadFromJsonAsync<CreateOrganisationEndpoint.CreateOrganisationResponse>();
        ArgumentNullException.ThrowIfNull(org);
        return org;
    }
}