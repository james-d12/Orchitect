using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Dispatch.Queue;

namespace Orchitect.Api.Endpoints.Engine.Deployment;

public sealed class CancelDeploymentEndpoint : IEndpoint
{
    private const int MaxAttempts = 2;

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/{id:guid}/cancel", HandleAsync)
        .RequireDeploymentAccess()
        .WithSummary(
            "Cancels the queued or running run of a deployment. A queued run is cancelled straight away. " +
            "A running runner is sent SIGTERM and given the stop grace period to exit, after which the " +
            "deployment and its run move to Cancelled.");

    public sealed record CancelDeploymentResponse(
        Guid Id,
        string Status,
        Guid RunId,
        string RunStatus,
        DateTime? CancelRequestedAt,
        Uri Location);

    private static async Task<Results<Accepted<CancelDeploymentResponse>, NotFound, Conflict<string>>>
        HandleAsync(
            [FromRoute]
            Guid id,
            [FromServices]
            IDeploymentRepository repository,
            [FromServices]
            IDeploymentRunRepository runRepository,
            [FromServices]
            IDeploymentRunCancellation runCancellation,
            [FromServices]
            IExecutor executor,
            HttpContext httpContext,
            CancellationToken cancellationToken)
    {
        var deployment = await repository.GetByIdAsync(new DeploymentId(id), cancellationToken);

        if (deployment is null)
        {
            return TypedResults.NotFound();
        }

        var run = await runRepository.GetLatestAsync(deployment.Id, cancellationToken);

        for (var attempt = 1; run is { IsActive: true }; attempt++)
        {
            try
            {
                if (run.Status == DeploymentRunStatus.Queued)
                {
                    run = await runRepository.UpdateAsync(run.Cancel(), cancellationToken) ?? run;

                    if (deployment.IsActive)
                    {
                        deployment = deployment.Cancel();
                        await repository.UpdateAsync(deployment, cancellationToken);
                    }
                }
                else
                {
                    run = await runRepository.UpdateAsync(run.RequestCancel(DateTime.UtcNow), cancellationToken) ??
                          run;

                    if (!runCancellation.RequestStop(run.Id))
                    {
                        await executor.SignalStopAsync(run.Id.Value.ToString(), cancellationToken);
                    }
                }

                return Accepted(httpContext, deployment, run);
            }
            catch (DeploymentRunConflictException) when (attempt < MaxAttempts)
            {
                run = await runRepository.GetByIdAsync(run.Id, cancellationToken);
            }
            catch (DeploymentRunConflictException)
            {
                return TypedResults.Conflict(
                    $"Run {run.Id.Value} of deployment with Id: {id} kept changing while it was being cancelled. Try again.");
            }
        }

        return TypedResults.Conflict($"Deployment with Id: {id} has no queued or running run to cancel.");
    }

    private static Accepted<CancelDeploymentResponse> Accepted(HttpContext httpContext,
        Orchitect.Domain.Engine.Deployment.Deployment deployment, DeploymentRun run)
    {
        var locationUrl =
            new Uri($"{httpContext.Request.Scheme}://{httpContext.Request.Host}/deployments/{deployment.Id.Value}");

        return TypedResults.Accepted(locationUrl,
            new CancelDeploymentResponse(deployment.Id.Value, deployment.Status.ToString(), run.Id.Value,
                run.Status.ToString(), run.CancelRequestedAt, locationUrl));
    }
}
