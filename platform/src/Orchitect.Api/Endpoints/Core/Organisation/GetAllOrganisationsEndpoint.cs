using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Endpoints.Core.Organisation;

public sealed class GetAllOrganisationsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", HandleAsync)
        .WithSummary("Get All Organisations.");

    public sealed record GetAllOrganisationsResponse(List<GetOrganisationEndpoint.GetOrganisationResponse> Organisations);

    private static async Task<Results<Ok<GetAllOrganisationsResponse>, InternalServerError>> HandleAsync(
        [FromServices]
        IOrganisationRepository repository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationIds = await repository.GetMemberOrganisationIdsAsync(user, cancellationToken);
        var organisations = repository.GetAll().Where(o => organisationIds.Contains(o.Id)).ToList();
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

