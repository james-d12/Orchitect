using Orchitect.Core.Api.Endpoints.Credential;
using Orchitect.Core.Api.Endpoints.Organisation;
using Orchitect.Core.Api.Endpoints.User;
using Orchitect.Shared;

namespace Orchitect.Core.Api.Endpoints;

public static class Endpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public void MapEndpoints()
        {
            endpoints.MapUserEndpoints();
            endpoints.MapOrganisationEndpoints();
            endpoints.MapCredentialEndpoints();
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

        private void MapCredentialEndpoints()
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