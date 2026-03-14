using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orchitect.Inventory.Api.Endpoints.Discovery;
using Orchitect.Shared;

namespace Orchitect.Inventory.Api.Endpoints;

public static class Endpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public void MapInventoryEndpoints()
        {
            var discoveryGroup = endpoints.MapGroup("/discovery")
                .RequireAuthorization()
                .WithTags("Discovery");

            discoveryGroup.MapEndpoint<CreateDiscoveryConfigurationEndpoint>();
            discoveryGroup.MapEndpoint<ListDiscoveryConfigurationsEndpoint>();
            discoveryGroup.MapEndpoint<UpdateDiscoveryConfigurationEndpoint>();
            discoveryGroup.MapEndpoint<DeleteDiscoveryConfigurationEndpoint>();
            discoveryGroup.MapEndpoint<TriggerDiscoveryEndpoint>();
        }
    }
}