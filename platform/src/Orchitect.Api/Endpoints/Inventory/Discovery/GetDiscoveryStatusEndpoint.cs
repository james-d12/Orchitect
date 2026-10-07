using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

namespace Orchitect.Api.Endpoints.Inventory.Discovery;

public sealed class GetDiscoveryStatusEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}/status", HandleAsync)
        .RequireOrganisationAccess<DiscoveryConfiguration, DiscoveryConfigurationId>(
            id => new DiscoveryConfigurationId(id))
        .WithName("GetDiscoveryStatus")
        .WithSummary("Gets the latest run of a discovery configuration")
        .Produces<DiscoveryRunResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<DiscoveryRunResponse>, NotFound<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IDiscoveryRunRepository runRepository,
        CancellationToken cancellationToken)
    {
        var run = await runRepository.GetLatestAsync(new DiscoveryConfigurationId(id), cancellationToken);

        if (run is null)
        {
            return TypedResults.NotFound(new ErrorResponse
            {
                Errors = [new Error { Code = "DISCOVERY_RUN_NOT_FOUND", Message = "Discovery has not run yet" }]
            });
        }

        return TypedResults.Ok(DiscoveryRunResponse.From(run));
    }
}
