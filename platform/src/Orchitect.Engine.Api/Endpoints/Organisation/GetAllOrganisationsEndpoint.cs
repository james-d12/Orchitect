using Orchitect.Engine.Domain.Organisation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Engine.Api.Common;

namespace Orchitect.Engine.Api.Endpoints.Organisation;

public sealed class GetAllOrganisationsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Get All Organisations.");

    private sealed record GetAllOrganisationsResponse(List<GetOrganisationEndpoint.GetOrganisationResponse> Organisations);

    private static Results<Ok<GetAllOrganisationsResponse>, InternalServerError> Handle(
        [FromServices]
        IOrganisationRepository repository)
    {
        var organisations = repository.GetAll().ToList();
        var organisationsResponse = organisations
            .Select(o => new GetOrganisationEndpoint.GetOrganisationResponse(
                o.Id.Value,
                o.Name,
                o.CreatedAt,
                o.UpdatedAt))
            .ToList();
        return TypedResults.Ok(new GetAllOrganisationsResponse(organisationsResponse));
    }
}

