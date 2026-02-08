using Orchitect.Engine.Domain.Organisation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Engine.Api.Common;

namespace Orchitect.Engine.Api.Endpoints.Organisation;

public sealed class DeleteOrganisationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id:guid}", HandleAsync)
        .WithSummary("Deletes an organisation by Id.");

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IOrganisationRepository repository,
        CancellationToken cancellationToken)
    {
        var organisationId = new OrganisationId(id);
        var deleted = await repository.DeleteAsync(organisationId, cancellationToken);

        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}

