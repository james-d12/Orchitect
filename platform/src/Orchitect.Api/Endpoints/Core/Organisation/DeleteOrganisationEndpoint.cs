using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

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