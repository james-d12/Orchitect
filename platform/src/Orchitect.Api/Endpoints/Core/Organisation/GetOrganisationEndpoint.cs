using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

public sealed class GetOrganisationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}", HandleAsync)
        .WithSummary("Gets an organisation by Id.");

    public sealed record GetOrganisationResponse(Guid Id, string Name, DateTime CreatedAt, DateTime UpdatedAt);

    private static async Task<Results<Ok<GetOrganisationResponse>, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IOrganisationRepository repository,
        CancellationToken cancellationToken)
    {
        var organisationId = new OrganisationId(id);
        var organisationResponse = await repository.GetByIdAsync(organisationId, cancellationToken);

        if (organisationResponse is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetOrganisationResponse(
            organisationResponse.Id.Value,
            organisationResponse.Name,
            organisationResponse.CreatedAt,
            organisationResponse.UpdatedAt));
    }
}

