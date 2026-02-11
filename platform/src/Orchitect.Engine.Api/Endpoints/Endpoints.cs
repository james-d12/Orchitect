using Orchitect.Engine.Api.Endpoints.Application;
using Orchitect.Engine.Api.Endpoints.Deployment;
using Orchitect.Engine.Api.Endpoints.Environment;
using Orchitect.Engine.Api.Endpoints.ResourceTemplate;
using Orchitect.Shared;

namespace Orchitect.Engine.Api.Endpoints;

public static class Endpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public void MapEndpoints()
        {
            endpoints.MapApplicationEndpoints();
            endpoints.MapEnvironmentEndpoints();
            endpoints.MapDeploymentEndpoints();
            endpoints.MapResourceTemplateEndpoints();
        }

        private void MapApplicationEndpoints()
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

        private void MapEnvironmentEndpoints()
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

        private void MapDeploymentEndpoints()
        {
            var endpoints1 = endpoints.MapGroup("/deployments")
                .WithTags("Deployment");

            endpoints1.MapPrivateGroup()
                .MapEndpoint<CreateDeploymentEndpoint>();
        }

        private void MapResourceTemplateEndpoints()
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

        private RouteGroupBuilder MapPrivateGroup(string? prefix = null)
        {
            return endpoints.MapGroup(prefix ?? string.Empty)
                .RequireAuthorization();
        }

        private IEndpointRouteBuilder MapEndpoint<TEndpoint>()
            where TEndpoint : IEndpoint
        {
            TEndpoint.Map(endpoints);
            return endpoints;
        }
    }
}