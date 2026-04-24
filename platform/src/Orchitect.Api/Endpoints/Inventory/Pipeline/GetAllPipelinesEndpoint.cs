using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.Pipeline.Requests;
using Orchitect.Domain.Inventory.Pipeline.Services;

namespace Orchitect.Api.Endpoints.Inventory.Pipeline;

public sealed class GetAllPipelinesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Gets all pipelines by query parameters.");

    public sealed record GetAllPipelinesResponse(
        List<GetPipelineEndpoint.GetPipelineResponse> Pipelines);

    public sealed record PipelineQueryRequest(
        Guid OrganisationId,
        string? Id = null,
        string? Name = null,
        string? Url = null,
        string? OwnerName = null,
        PipelinePlatform? Platform = null);

    private static Results<Ok<GetAllPipelinesResponse>, NotFound> Handle(
        [AsParameters]
        PipelineQueryRequest query,
        [FromServices]
        IPipelineRepository repository)
    {
        var organisationId = new OrganisationId(query.OrganisationId);

        var pipelineQuery = new PipelineQuery(
            OrganisationId: organisationId,
            Id: query.Id,
            Name: query.Name,
            Url: query.Url,
            OwnerName: query.OwnerName,
            Platform: query.Platform);

        var pipelines = repository.GetByQuery(pipelineQuery);

        var response = pipelines
            .Select(p => new GetPipelineEndpoint.GetPipelineResponse(
                p.Id.Value,
                p.Name,
                p.Url,
                p.User.Name,
                p.Platform,
                p.DiscoveredAt,
                p.UpdatedAt))
            .ToList();

        return TypedResults.Ok(new GetAllPipelinesResponse(response));
    }
}
