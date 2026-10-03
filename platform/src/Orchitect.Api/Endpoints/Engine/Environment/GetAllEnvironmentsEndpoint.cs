using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Engine.Environment;

namespace Orchitect.Api.Endpoints.Engine.Environment;

public sealed class GetAllEnvironmentsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", HandleAsync)
        .HandlesOrganisationScope()
        .WithSummary("Get All Environments.");

    public sealed record GetAllEnvironmentsResponse(List<GetEnvironmentEndpoint.GetEnvironmentResponse> Environments);

    private static async Task<Results<Ok<GetAllEnvironmentsResponse>, InternalServerError>> HandleAsync(
        [FromServices]
        IEnvironmentRepository repository,
        [FromServices]
        IOrganisationAccess organisationAccess,
        CancellationToken cancellationToken)
    {
        var organisationIds = await organisationAccess.GetOrganisationIdsAsync(cancellationToken);
        var environments = await repository.GetByOrganisationIdsAsync(organisationIds, cancellationToken);
        var environmentsResponse = environments
            .Select(r => new GetEnvironmentEndpoint.GetEnvironmentResponse(r.Id.Value, r.Name))
            .ToList();
        return TypedResults.Ok(new GetAllEnvironmentsResponse(environmentsResponse));
    }
}