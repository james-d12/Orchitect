using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Engine.Application;

namespace Orchitect.Api.Endpoints.Engine.Application;

public sealed class UpdateApplicationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPut("/{id:guid}", HandleAsync)
        .RequireOrganisationAccess<Orchitect.Domain.Engine.Application.Application, Orchitect.Domain.Engine.Application.ApplicationId>(
            id => new Orchitect.Domain.Engine.Application.ApplicationId(id))
        .WithSummary("Updates an existing application.");

    public sealed record UpdateApplicationRequest(string Name, UpdateRepositoryRequest Repository);

    public sealed record UpdateRepositoryRequest(string Name, Uri Url, RepositoryProvider Provider);

    public sealed record UpdateApplicationResponse(
        Guid Id,
        string Name,
        Repository Repository,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private static async Task<Results<Ok<UpdateApplicationResponse>, BadRequest<string>, NotFound, InternalServerError>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromBody]
        UpdateApplicationRequest request,
        [FromServices]
        IApplicationRepository repository,
        CancellationToken cancellationToken)
    {
        var applicationId = new Orchitect.Domain.Engine.Application.ApplicationId(id);
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

        Orchitect.Domain.Engine.Application.Application updatedApplication;

        try
        {
            updatedApplication = existingApplication.Update(request.Name, updatedRepository);
        }
        catch (ArgumentException exception)
        {
            return TypedResults.BadRequest(exception.Message);
        }

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