using Conductor.Engine.Api.Common;
using Conductor.Engine.Domain.Organisation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Conductor.Engine.Api.Endpoints.Organisation;

public sealed class UpdateOrganisationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id:guid}", HandleAsync)
        .WithSummary("Updates an existing organisation.");

    private sealed record UpdateOrganisationRequest(string Name);

    private sealed record UpdateOrganisationResponse(Guid Id, string Name, DateTime CreatedAt, DateTime UpdatedAt);

    private static async Task<Results<Ok<UpdateOrganisationResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        UpdateOrganisationRequest request,
        [FromServices]
        IOrganisationRepository repository,
        CancellationToken cancellationToken)
    {
        var organisationId = new OrganisationId(id);
        var existingOrganisation = await repository.GetByIdAsync(organisationId, cancellationToken);

        if (existingOrganisation is null)
        {
            return TypedResults.NotFound();
        }

        var updatedOrganisation = existingOrganisation.Update(request.Name);
        var organisationResponse = await repository.UpdateAsync(updatedOrganisation, cancellationToken);

        if (organisationResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new UpdateOrganisationResponse(
            organisationResponse.Id.Value,
            organisationResponse.Name,
            organisationResponse.CreatedAt,
            organisationResponse.UpdatedAt));
    }
}

