using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Shared;

namespace Orchitect.Api.Endpoints.Engine.ResourceTemplate;

public sealed class DeleteResourceTemplateEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id:guid}", HandleAsync)
        .WithSummary("Deletes a resource template by Id.");

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IResourceTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var resourceTemplateId = new ResourceTemplateId(id);
        var deleted = await repository.DeleteAsync(resourceTemplateId, cancellationToken);

        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
