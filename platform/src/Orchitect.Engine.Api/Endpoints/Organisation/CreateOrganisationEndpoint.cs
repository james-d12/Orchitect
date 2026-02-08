using Orchitect.Engine.Domain.Organisation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Engine.Api.Common;

namespace Orchitect.Engine.Api.Endpoints.Organisation;

public sealed class CreateOrganisationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new organisation.");

    private sealed record CreateOrganisationResponse(Guid Id, string Name);

    private static async Task<Results<Ok<CreateOrganisationResponse>, InternalServerError>> HandleAsync(
        [FromBody]
        CreateOrganisationRequest request,
        [FromServices]
        IOrganisationRepository repository,
        CancellationToken cancellationToken)
    {
        var organisation = Orchitect.Engine.Domain.Organisation.Organisation.Create(request.Name);
        var organisationResponse = await repository.CreateAsync(organisation, cancellationToken);

        if (organisationResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new CreateOrganisationResponse(organisationResponse.Id.Value,
            organisationResponse.Name));
    }
}