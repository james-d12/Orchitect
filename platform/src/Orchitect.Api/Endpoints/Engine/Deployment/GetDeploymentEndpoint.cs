using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Api.Endpoints.Engine.Deployment;

public sealed class GetDeploymentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id:guid}", HandleAsync)
        .RequireDeploymentAccess()
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
        string? ErrorSummary,
        GetDeploymentRunResponse? LatestRun);

    public sealed record GetDeploymentRunResponse(
        Guid Id,
        string Operation,
        string Status,
        DateTime QueuedAt,
        DateTime? StartedAt,
        DateTime? FinishedAt,
        long? ExitCode,
        string? ErrorSummary,
        DateTime? CancelRequestedAt);

    private static async Task<Results<Ok<GetDeploymentResponse>, NotFound>> HandleAsync(
        [FromRoute]
        Guid id,
        [FromServices]
        IDeploymentRepository repository,
        [FromServices]
        IDeploymentRunRepository runRepository,
        CancellationToken cancellationToken)
    {
        var deployment = await repository.GetByIdAsync(new DeploymentId(id), cancellationToken);

        if (deployment is null)
        {
            return TypedResults.NotFound();
        }

        var run = await runRepository.GetLatestAsync(deployment.Id, cancellationToken);

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
            deployment.ErrorSummary,
            run is null
                ? null
                : new GetDeploymentRunResponse(
                    run.Id.Value,
                    run.Operation.ToString(),
                    run.Status.ToString(),
                    run.QueuedAt,
                    run.StartedAt,
                    run.FinishedAt,
                    run.ExitCode,
                    run.ErrorSummary,
                    run.CancelRequestedAt)));
    }
}
