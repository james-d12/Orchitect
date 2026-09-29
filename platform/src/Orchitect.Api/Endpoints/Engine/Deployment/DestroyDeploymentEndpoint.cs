using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Infrastructure.Engine.Queue;

namespace Orchitect.Api.Endpoints.Engine.Deployment;

public sealed class DestroyDeploymentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapDelete("/{id:guid}", HandleAsync)
        .WithSummary(
            "Destroys the resources of the latest deployment of an application to an environment. " +
            "The deployment record is kept and moves to Destroyed.");

    public sealed record DestroyDeploymentResponse(Guid Id, string Status, Uri Location);

    private static async Task<Results<Accepted<DestroyDeploymentResponse>, NotFound, Conflict<string>>>
        HandleAsync(
            [FromRoute]
            Guid id,
            [FromServices]
            IDeploymentRepository repository,
            [FromServices]
            IDeploymentQueue deploymentQueue,
            HttpContext httpContext,
            CancellationToken cancellationToken)
    {
        var deployment = await repository.GetByIdAsync(new DeploymentId(id), cancellationToken);

        if (deployment is null)
        {
            return TypedResults.NotFound();
        }

        if (!deployment.CanDestroy)
        {
            return TypedResults.Conflict(
                $"Deployment with Id: {id} cannot be destroyed while {deployment.Status}.");
        }

        var latest = await repository.GetLatestAsync(deployment.ApplicationId, deployment.EnvironmentId,
            cancellationToken);

        if (latest?.Id != deployment.Id)
        {
            return TypedResults.Conflict(
                $"Deployment with Id: {id} is not the latest deployment of its application to its environment.");
        }

        await deploymentQueue.QueueDeploymentTaskAsync(
            new DeploymentQueueRequest(deployment.ApplicationId, deployment.Id, DeploymentOperation.Destroy),
            cancellationToken);

        var locationUrl =
            new Uri($"{httpContext.Request.Scheme}://{httpContext.Request.Host}/deployments/{deployment.Id.Value}");

        return TypedResults.Accepted(locationUrl,
            new DestroyDeploymentResponse(deployment.Id.Value, deployment.Status.ToString(), locationUrl));
    }
}
