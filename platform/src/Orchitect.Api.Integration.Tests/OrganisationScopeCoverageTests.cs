using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Api.Integration.Tests.Helpers;
using Orchitect.Api.Shared.Authorization;

namespace Orchitect.Api.Integration.Tests;

[Collection("Integration")]
public sealed class OrganisationScopeCoverageTests(WebApplicationFactoryWithPostgres factory)
{
    [Fact]
    public void AuthorisedEndpoints_ShouldDeclareHowTheyScopeOrganisations()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is not null &&
                        e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .ToList();

        var unscoped = endpoints
            .Where(e => e.Metadata.GetMetadata<OrganisationScopedMetadata>() is null &&
                        e.Metadata.GetMetadata<HandlesOrganisationScopeMetadata>() is null)
            .Select(e => e.DisplayName)
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.Empty(unscoped);
    }
}