using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;

namespace Orchitect.Api.Endpoints.Engine.Resource;

public sealed class GetAllResourcesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", HandleAsync)
        .HandlesOrganisationScope()
        .WithSummary("Gets all resources, optionally only those in an environment.");

    public sealed record GetAllResourcesResponse(List<ResourceResponse> Resources);

    private static async Task<Ok<GetAllResourcesResponse>> HandleAsync(
        [FromQuery]
        Guid? environmentId,
        [FromServices]
        IResourceRepository repository,
        [FromServices]
        IOrganisationAccess organisationAccess,
        CancellationToken cancellationToken)
    {
        var organisationIds = await organisationAccess.GetOrganisationIdsAsync(cancellationToken);
        var resources = await repository.GetByOrganisationIdsAsync(organisationIds,
            environmentId is null ? null : new EnvironmentId(environmentId.Value), cancellationToken);
        return TypedResults.Ok(new GetAllResourcesResponse(resources.Select(ResourceResponse.From).ToList()));
    }
}
