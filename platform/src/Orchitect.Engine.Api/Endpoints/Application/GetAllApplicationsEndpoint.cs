using Orchitect.Engine.Domain.Application;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Shared;

namespace Orchitect.Engine.Api.Endpoints.Application;

public sealed class GetAllApplicationsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Get All Applications.");

    private sealed record GetAllApplicationsResponse(List<GetApplicationEndpoint.GetApplicationResponse> Applications);

    private static Results<Ok<GetAllApplicationsResponse>, InternalServerError> Handle(
        [FromServices]
        IApplicationRepository repository)
    {
        var applications = repository.GetAll().ToList();
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