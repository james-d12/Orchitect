using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Endpoints.Inventory.Discovery;
using Orchitect.Api.Shared;

namespace Orchitect.Api.Endpoints.Inventory;

public static class Endpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
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