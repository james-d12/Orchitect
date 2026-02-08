using Orchitect.Engine.Domain.Environment;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Engine.Api.Common;

namespace Orchitect.Engine.Api.Endpoints.Environment;

public sealed class GetAllEnvironmentsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Get All Environments.");

    private sealed record GetAllEnvironmentsResponse(List<GetEnvironmentEndpoint.GetEnvironmentResponse> Environments);

    private static Results<Ok<GetAllEnvironmentsResponse>, InternalServerError> Handle(
        [FromServices]
        IEnvironmentRepository repository)
    {
        var environments = repository.GetAll().ToList();
        var environmentsResponse = environments
            .Select(r => new GetEnvironmentEndpoint.GetEnvironmentResponse(r.Id.Value, r.Name))
            .ToList();
        return TypedResults.Ok(new GetAllEnvironmentsResponse(environmentsResponse));
    }
}