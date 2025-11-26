using Conductor.Engine.Api.Common;
using Conductor.Engine.Domain.ResourceTemplate;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Conductor.Engine.Api.Endpoints.ResourceTemplate;

public sealed class GetResourceTemplateEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}", HandleAsync)
        .WithSummary("Gets an existing resource template by id.");

    public sealed record GetResourceTemplateResponse(Guid Id, string Name, string Type, string Description);

    private static async Task<Results<Ok<GetResourceTemplateResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IResourceTemplateRepository repository,
        [FromServices]
        ILogger<GetResourceTemplateEndpoint> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var resourceTemplateId = new ResourceTemplateId(id);
            var resourceTemplateResponse = await repository.GetByIdAsync(resourceTemplateId, cancellationToken);

            if (resourceTemplateResponse is null)
            {
                return TypedResults.NotFound();
            }

            var response = new GetResourceTemplateResponse(
                Id: resourceTemplateResponse.Id.Value,
                Name: resourceTemplateResponse.Name,
                Type: resourceTemplateResponse.Type,
                Description: resourceTemplateResponse.Description
            );

            return TypedResults.Ok(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not retrieve Resource Template for {Id}.", id);
            return TypedResults.InternalServerError();
        }
    }
}