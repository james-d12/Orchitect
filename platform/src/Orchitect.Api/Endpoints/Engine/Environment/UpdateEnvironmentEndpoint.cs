using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.Environment;

namespace Orchitect.Api.Endpoints.Engine.Environment;

public sealed class UpdateEnvironmentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id:guid}", HandleAsync)
        .WithSummary("Updates an existing environment.");

    private sealed record UpdateEnvironmentRequest(string Name, string Description);

    private sealed record UpdateEnvironmentResponse(Guid Id, string Name, string Description, DateTime CreatedAt, DateTime UpdatedAt);

    private static async Task<Results<Ok<UpdateEnvironmentResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        UpdateEnvironmentRequest request,
        [FromServices]
        IEnvironmentRepository repository,
        CancellationToken cancellationToken)
    {
        var environmentId = new EnvironmentId(id);
        var existingEnvironment = await repository.GetByIdAsync(environmentId, cancellationToken);

        if (existingEnvironment is null)
        {
            return TypedResults.NotFound();
        }

        var updatedEnvironment = existingEnvironment.Update(request.Name, request.Description);
        var environmentResponse = await repository.UpdateAsync(updatedEnvironment, cancellationToken);

        if (environmentResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new UpdateEnvironmentResponse(
            environmentResponse.Id.Value,
            environmentResponse.Name,
            environmentResponse.Description,
            environmentResponse.CreatedAt,
            environmentResponse.UpdatedAt));
    }
}
