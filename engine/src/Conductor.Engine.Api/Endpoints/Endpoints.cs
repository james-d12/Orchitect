using Conductor.Engine.Api.Common;
using Conductor.Engine.Api.Endpoints.Application;
using Conductor.Engine.Api.Endpoints.Deployment;
using Conductor.Engine.Api.Endpoints.Environment;
using Conductor.Engine.Api.Endpoints.ResourceTemplate;
using Conductor.Engine.Api.Endpoints.User;

namespace Conductor.Engine.Api.Endpoints;

public static class Endpoints
{
    public static void MapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapApplicationEndpoints();
        endpoints.MapEnvironmentEndpoints();
        endpoints.MapDeploymentEndpoints();
        endpoints.MapResourceTemplateEndpoints();
        endpoints.MapUserEndpoints();
    }

    private static void MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("/applications")
            .WithTags("Application");

        endpoints.MapPrivateGroup()
            .MapEndpoint<CreateApplicationEndpoint>()
            .MapEndpoint<GetAllApplicationsEndpoint>()
            .MapEndpoint<GetApplicationEndpoint>();
    }

    private static void MapEnvironmentEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("/environments")
            .WithTags("Environment");

        endpoints.MapPrivateGroup()
            .MapEndpoint<CreateEnvironmentEndpoint>()
            .MapEndpoint<GetAllEnvironmentsEndpoint>()
            .MapEndpoint<GetEnvironmentEndpoint>();
    }

    private static void MapDeploymentEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("/deployments")
            .WithTags("Deployment");

        endpoints.MapPrivateGroup()
            .MapEndpoint<CreateDeploymentEndpoint>();
    }

    private static void MapResourceTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("/resource-templates")
            .WithTags("Resource Template");

        endpoints.MapPrivateGroup()
            .MapEndpoint<CreateResourceTemplateEndpoint>()
            .MapEndpoint<CreateResourceTemplateWithVersionEndpoint>()
            .MapEndpoint<GetResourceTemplateEndpoint>()
            .MapEndpoint<GetAllResourceTemplatesEndpoint>();
    }

    private static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("/users")
            .WithTags("User");

        endpoints.MapPublicGroup()
            .MapEndpoint<RegisterUserEndpoint>()
            .MapEndpoint<LoginUserEndpoint>();
    }

    private static RouteGroupBuilder MapPublicGroup(this IEndpointRouteBuilder app, string? prefix = null)
    {
        return app.MapGroup(prefix ?? string.Empty)
            .AllowAnonymous();
    }

    private static RouteGroupBuilder MapPrivateGroup(this IEndpointRouteBuilder app, string? prefix = null)
    {
        return app.MapGroup(prefix ?? string.Empty)
            .RequireAuthorization();
    }

    private static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app)
        where TEndpoint : IEndpoint
    {
        TEndpoint.Map(app);
        return app;
    }
}