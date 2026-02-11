using Orchitect.Core.Application.Organisation;
using Orchitect.Core.Application.User;
using Orchitect.Shared;

namespace Orchitect.Core.Application.Endpoints;

public static class Endpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public void MapEndpoints()
        {
            endpoints.MapUserEndpoints();
            endpoints.MapOrganisationEndpoints();
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