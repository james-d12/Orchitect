using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Endpoints.Engine.Application;
using Orchitect.Api.Endpoints.Engine.Deployment;
using Orchitect.Api.Endpoints.Engine.Environment;
using Orchitect.Api.Endpoints.Engine.ResourceTemplate;
using Orchitect.Api.Shared;

namespace Orchitect.Api.Endpoints.Engine;

public static class Endpoints
{
    public static void MapEngineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapApplicationEndpoints();
        endpoints.MapEnvironmentEndpoints();
        endpoints.MapDeploymentEndpoints();
        endpoints.MapResourceTemplateEndpoints();
    }

    private static void MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpoints1 = endpoints.MapGroup("/applications")
            .WithTags("Application");

        endpoints1.MapPrivateGroup()
            .MapEndpoint<CreateApplicationEndpoint>()
            .MapEndpoint<GetAllApplicationsEndpoint>()
            .MapEndpoint<GetApplicationEndpoint>()
            .MapEndpoint<UpdateApplicationEndpoint>()
            .MapEndpoint<DeleteApplicationEndpoint>();
    }

    private static void MapEnvironmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpoints1 = endpoints.MapGroup("/environments")
            .WithTags("Environment");

        endpoints1.MapPrivateGroup()
            .MapEndpoint<CreateEnvironmentEndpoint>()
            .MapEndpoint<GetAllEnvironmentsEndpoint>()
            .MapEndpoint<GetEnvironmentEndpoint>()
            .MapEndpoint<UpdateEnvironmentEndpoint>()
            .MapEndpoint<DeleteEnvironmentEndpoint>();
    }

    private static void MapDeploymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpoints1 = endpoints.MapGroup("/deployments")
            .WithTags("Deployment");

        endpoints1.MapPrivateGroup()
            .MapEndpoint<CreateDeploymentEndpoint>();
    }

    private static void MapResourceTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpoints1 = endpoints.MapGroup("/resource-templates")
            .WithTags("Resource Template");

        endpoints1.MapPrivateGroup()
            .MapEndpoint<CreateResourceTemplateEndpoint>()
            .MapEndpoint<CreateResourceTemplateWithVersionEndpoint>()
            .MapEndpoint<GetResourceTemplateEndpoint>()
            .MapEndpoint<GetAllResourceTemplatesEndpoint>()
            .MapEndpoint<UpdateResourceTemplateEndpoint>()
            .MapEndpoint<DeleteResourceTemplateEndpoint>();
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