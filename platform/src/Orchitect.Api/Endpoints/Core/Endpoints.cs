using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Endpoints.Core.Credential;
using Orchitect.Api.Endpoints.Core.Organisation;
using Orchitect.Api.Endpoints.Core.User;
using Orchitect.Shared;

namespace Orchitect.Api.Endpoints.Core;

public static class Endpoints
{
    public static void MapCoreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapUserEndpoints();
        endpoints.MapOrganisationEndpoints();
        endpoints.MapCredentialEndpoints();
    }

    private static void MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpoints1 = endpoints.MapGroup("/users")
            .WithTags("User");

        endpoints1.MapPublicGroup()
            .MapEndpoint<RegisterUserEndpoint>()
            .MapEndpoint<LoginUserEndpoint>();
    }

    private static void MapOrganisationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpoints1 = endpoints.MapGroup("/organisations")
            .WithTags("Organisation");

        endpoints1.MapPrivateGroup()
            .MapEndpoint<CreateOrganisationEndpoint>()
            .MapEndpoint<GetAllOrganisationsEndpoint>()
            .MapEndpoint<GetOrganisationEndpoint>()
            .MapEndpoint<UpdateOrganisationEndpoint>()
            .MapEndpoint<DeleteOrganisationEndpoint>();
    }

    private static void MapCredentialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpoints1 = endpoints.MapGroup("/credentials")
            .WithTags("Credential");

        endpoints1.MapPrivateGroup()
            .MapEndpoint<CreateCredentialEndpoint>()
            .MapEndpoint<GetAllCredentialsEndpoint>()
            .MapEndpoint<GetCredentialEndpoint>()
            .MapEndpoint<UpdateCredentialEndpoint>()
            .MapEndpoint<DeleteCredentialEndpoint>();
    }

    private static RouteGroupBuilder MapPublicGroup(this IEndpointRouteBuilder endpoints, string? prefix = null)
    {
        return endpoints.MapGroup(prefix ?? string.Empty)
            .AllowAnonymous();
    }

    private static RouteGroupBuilder MapPrivateGroup(this IEndpointRouteBuilder endpoints, string? prefix = null)
    {
        return endpoints.MapGroup(prefix ?? string.Empty)
            .RequireAuthorization();
    }

    private static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder endpoints)
        where TEndpoint : IEndpoint
    {
        TEndpoint.Map(endpoints);
        return endpoints;
    }
}