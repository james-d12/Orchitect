using AutoFixture;
using Orchitect.Api.Endpoints.Engine.Application;
using Orchitect.Domain.Engine.Application;

namespace Orchitect.Api.Integration.Tests.Helpers;

public static class ApplicationSetupHelper
{
    private static readonly Fixture Fixture = new();
    private const string ApplicationsUrl = "/applications";

    public static async Task<CreateApplicationEndpoint.CreateApplicationResponse> CreateApplicationAsync(
        this HttpClient client, Guid organisationId)
    {
        var request = new CreateApplicationRequest(
            Fixture.Create<string>(),
            organisationId.ToString(),
            new CreateRepositoryRequest(
                Fixture.Create<string>(),
                new Uri("https://github.com/test/repo"),
                Fixture.Create<RepositoryProvider>()));

        var response = await client.PostAsJsonAsync(ApplicationsUrl, request);
        var created = await response.ReadFromJsonAsync<CreateApplicationEndpoint.CreateApplicationResponse>();
        ArgumentNullException.ThrowIfNull(created);
        return created;
    }
}
