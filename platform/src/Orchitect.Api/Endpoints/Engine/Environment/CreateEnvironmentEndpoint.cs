using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Shared;

namespace Orchitect.Api.Endpoints.Engine.Environment;

public sealed class CreateEnvironmentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new environment.");

    private sealed record CreateEnvironmentResponse(Guid Id);

    private static async Task<Results<Ok<CreateEnvironmentResponse>, InternalServerError>> HandleAsync(
        [FromBody]
        CreateEnvironmentRequest request,
        [FromServices]
        IEnvironmentRepository repository,
        CancellationToken cancellationToken)
    {
        var environment = Orchitect.Domain.Engine.Environment.Environment.Create(request);
        var environmentResponse = await repository.CreateAsync(environment, cancellationToken);

        if (environmentResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new CreateEnvironmentResponse(environmentResponse.Id.Value));
    }
}