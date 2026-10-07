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

public sealed class ListDiscoveryRunsEndpoint : IEndpoint
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}/runs", HandleAsync)
        .RequireOrganisationAccess<DiscoveryConfiguration, DiscoveryConfigurationId>(
            id => new DiscoveryConfigurationId(id))
        .WithName("ListDiscoveryRuns")
        .WithSummary("Lists the most recent runs of a discovery configuration, newest first")
        .Produces<IReadOnlyList<DiscoveryRunResponse>>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

    private static async Task<Results<Ok<IReadOnlyList<DiscoveryRunResponse>>, BadRequest<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromQuery]
        int? limit,
        [FromServices]
        IDiscoveryRunRepository runRepository,
        CancellationToken cancellationToken)
    {
        var take = limit ?? DefaultLimit;

        if (take is < 1 or > MaxLimit)
        {
            return TypedResults.BadRequest(new ErrorResponse
            {
                Errors = [new Error { Code = "INVALID_LIMIT", Message = $"Limit must be between 1 and {MaxLimit}" }]
            });
        }

        var runs = await runRepository.GetRecentAsync(new DiscoveryConfigurationId(id), take, cancellationToken);

        return TypedResults.Ok<IReadOnlyList<DiscoveryRunResponse>>(runs.Select(DiscoveryRunResponse.From).ToList());
    }
}
