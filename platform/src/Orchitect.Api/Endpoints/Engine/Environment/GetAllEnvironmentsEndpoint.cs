using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.Environment;

namespace Orchitect.Api.Endpoints.Engine.Environment;

public sealed class GetAllEnvironmentsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Get All Environments.");

    public sealed record GetAllEnvironmentsResponse(List<GetEnvironmentEndpoint.GetEnvironmentResponse> Environments);

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