using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.Pipeline.Services;

namespace Orchitect.Api.Endpoints.Inventory.Pipeline;

public sealed class GetPipelineEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id}", HandleAsync)
        .WithSummary("Gets a pipeline by id.");

    public sealed record GetPipelineResponse(
        string Id,
        string Name,
        Uri Url,
        string OwnerName,
        PipelinePlatform Platform,
        DateTime DiscoveredAt,
        DateTime UpdatedAt);

    private static async Task<Results<Ok<GetPipelineResponse>, NotFound>> HandleAsync(
        [FromRoute]
        string id,
        [FromServices]
        IPipelineRepository repository,
        CancellationToken cancellationToken)
    {
        var pipelineId = new PipelineId(id);
        var pipeline = await repository.GetByIdAsync(pipelineId, cancellationToken);

        if (pipeline is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetPipelineResponse(
            pipeline.Id.Value,
            pipeline.Name,
            pipeline.Url,
            pipeline.User.Name,
            pipeline.Platform,
            pipeline.DiscoveredAt,
            pipeline.UpdatedAt));
    }
}
