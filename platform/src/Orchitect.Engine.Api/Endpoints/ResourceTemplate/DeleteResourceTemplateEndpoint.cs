using Orchitect.Engine.Domain.ResourceTemplate;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Engine.Api.Common;

namespace Orchitect.Engine.Api.Endpoints.ResourceTemplate;

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
