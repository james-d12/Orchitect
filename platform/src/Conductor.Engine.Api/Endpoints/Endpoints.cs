using Conductor.Engine.Api.Common;
using Conductor.Engine.Api.Endpoints.Application;
using Conductor.Engine.Api.Endpoints.Deployment;
using Conductor.Engine.Api.Endpoints.Environment;
using Conductor.Engine.Api.Endpoints.Organisation;
using Conductor.Engine.Api.Endpoints.ResourceTemplate;
using Conductor.Engine.Api.Endpoints.User;

namespace Conductor.Engine.Api.Endpoints;

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
            endpoints.MapUserEndpoints();
            endpoints.MapOrganisationEndpoints();
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

        private void MapUserEndpoints()
        {
            var endpoints1 = endpoints.MapGroup("/users")
                .WithTags("User");

            endpoints1.MapPublicGroup()
                .MapEndpoint<RegisterUserEndpoint>()
                .MapEndpoint<LoginUserEndpoint>();
        }

        private void MapOrganisationEndpoints()
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

        private RouteGroupBuilder MapPublicGroup(string? prefix = null)
        {
            return endpoints.MapGroup(prefix ?? string.Empty)
                .AllowAnonymous();
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