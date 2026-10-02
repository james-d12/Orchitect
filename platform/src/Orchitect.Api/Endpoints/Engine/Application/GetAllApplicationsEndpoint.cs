using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;

namespace Orchitect.Api.Endpoints.Engine.Application;

public sealed class GetAllApplicationsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", HandleAsync)
        .WithSummary("Get All Applications.");

    public sealed record GetAllApplicationsResponse(List<GetApplicationEndpoint.GetApplicationResponse> Applications);

    private static async Task<Results<Ok<GetAllApplicationsResponse>, InternalServerError>> HandleAsync(
        [FromServices]
        IApplicationRepository repository,
        [FromServices]
        IOrganisationRepository organisationRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var organisationIds = await organisationRepository.GetMemberOrganisationIdsAsync(user, cancellationToken);
        var applications = repository.GetAll()
            .Where(r => organisationIds.Contains(r.OrganisationId))
            .ToList();
        var applicationsResponse = applications
            .Select(application => new GetApplicationEndpoint.GetApplicationResponse(
                application.Id.Value,
                application.Name,
                application.Repository.Name,
                application.Repository.Url.ToString(),
                application.CreatedAt,
                application.UpdatedAt
            ))
            .ToList();
        return TypedResults.Ok(new GetAllApplicationsResponse(applicationsResponse));
    }
}