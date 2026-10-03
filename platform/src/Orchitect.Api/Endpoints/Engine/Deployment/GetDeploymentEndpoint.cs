using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Api.Endpoints.Engine.Deployment;

public sealed class GetDeploymentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}", HandleAsync)
        .WithSummary("Gets a deployment and its status by Id.");

    public sealed record GetDeploymentResponse(
        Guid Id,
        Guid ApplicationId,
        Guid EnvironmentId,
        string CommitId,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string RequestedBy,
        DateTime? StartedAt,
        DateTime? CompletedAt,
        string? ErrorSummary);

    private static async Task<Results<Ok<GetDeploymentResponse>, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IDeploymentRepository repository,
        [FromServices]
        IApplicationRepository applicationRepository,
        [FromServices]
        IOrganisationRepository organisationRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var deployment = await repository.GetByIdAsync(new DeploymentId(id), cancellationToken);

        if (deployment is null)
        {
            return TypedResults.NotFound();
        }

        var application = await applicationRepository.GetByIdAsync(deployment.ApplicationId, cancellationToken);

        if (application is null ||
            !await organisationRepository.IsMemberAsync(user, application.OrganisationId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetDeploymentResponse(
            deployment.Id.Value,
            deployment.ApplicationId.Value,
            deployment.EnvironmentId.Value,
            deployment.CommitId.Value,
            deployment.Status.ToString(),
            deployment.CreatedAt,
            deployment.UpdatedAt,
            deployment.RequestedBy,
            deployment.StartedAt,
            deployment.CompletedAt,
            deployment.ErrorSummary));
    }
}
