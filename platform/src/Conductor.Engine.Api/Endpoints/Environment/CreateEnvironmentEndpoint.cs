using Conductor.Engine.Api.Common;
using Conductor.Engine.Domain.Environment;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Conductor.Engine.Api.Endpoints.Environment;

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
        var environment = Engine.Domain.Environment.Environment.Create(request);
        var environmentResponse = await repository.CreateAsync(environment, cancellationToken);

        if (environmentResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new CreateEnvironmentResponse(environmentResponse.Id.Value));
    }
}