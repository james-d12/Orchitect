using Orchitect.Engine.Domain.Application;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Engine.Api.Common;

namespace Orchitect.Engine.Api.Endpoints.Application;

public sealed class UpdateApplicationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id:guid}", HandleAsync)
        .WithSummary("Updates an existing application.");

    private sealed record UpdateApplicationRequest(string Name, UpdateRepositoryRequest Repository);

    private sealed record UpdateRepositoryRequest(string Name, Uri Url, RepositoryProvider Provider);

    private sealed record UpdateApplicationResponse(Guid Id, string Name, Repository Repository, DateTime CreatedAt, DateTime UpdatedAt);

    private static async Task<Results<Ok<UpdateApplicationResponse>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        UpdateApplicationRequest request,
        [FromServices]
        IApplicationRepository repository,
        CancellationToken cancellationToken)
    {
        var applicationId = new Orchitect.Engine.Domain.Application.ApplicationId(id);
        var existingApplication = await repository.GetByIdAsync(applicationId, cancellationToken);

        if (existingApplication is null)
        {
            return TypedResults.NotFound();
        }

        var updatedRepository = new Repository
        {
            Name = request.Repository.Name,
            Url = request.Repository.Url,
            Provider = request.Repository.Provider
        };

        var updatedApplication = existingApplication.Update(request.Name, updatedRepository);
        var applicationResponse = await repository.UpdateAsync(updatedApplication, cancellationToken);

        if (applicationResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        return TypedResults.Ok(new UpdateApplicationResponse(
            applicationResponse.Id.Value,
            applicationResponse.Name,
            applicationResponse.Repository,
            applicationResponse.CreatedAt,
            applicationResponse.UpdatedAt));
    }
}
