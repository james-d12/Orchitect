using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

public sealed class CreateOrganisationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new organisation.");

    public sealed record CreateOrganisationResponse(Guid Id, string Name);

    private static async Task<Results<Ok<CreateOrganisationResponse>, InternalServerError>> HandleAsync(
        [FromBody]
        CreateOrganisationRequest request,
        [FromServices]
        IOrganisationRepository repository,
        CancellationToken cancellationToken)
    {
        var organisation = Orchitect.Domain.Core.Organisation.Organisation.Create(request.Name);
        var organisationResponse = await repository.CreateAsync(organisation, cancellationToken);

        if (organisationResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new CreateOrganisationResponse(organisationResponse.Id.Value,
            organisationResponse.Name));
    }
}