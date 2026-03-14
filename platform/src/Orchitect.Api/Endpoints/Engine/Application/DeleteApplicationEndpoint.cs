using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Domain.Engine.Application;
using Orchitect.Shared;

namespace Orchitect.Api.Endpoints.Engine.Application;

public sealed class DeleteApplicationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id:guid}", HandleAsync)
        .WithSummary("Deletes an application by Id.");

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IApplicationRepository repository,
        CancellationToken cancellationToken)
    {
        var applicationId = new Orchitect.Domain.Engine.Application.ApplicationId(id);
        var deleted = await repository.DeleteAsync(applicationId, cancellationToken);

        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}