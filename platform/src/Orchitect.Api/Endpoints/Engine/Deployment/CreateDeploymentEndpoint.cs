using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Infrastructure.Engine.Queue;

namespace Orchitect.Api.Endpoints.Engine.Deployment;

public sealed class CreateDeploymentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new deployment into an environment for an application with a given commit id.");

    private sealed record CreateDeploymentResponse(Guid Id, string Status, Uri Location);

    private static async Task<Results<Accepted<CreateDeploymentResponse>, BadRequest<string>, InternalServerError>>
        HandleAsync(
            [FromBody]
            CreateDeploymentRequest request,
            [FromServices]
            IDeploymentRepository repository,
            [FromServices]
            IApplicationRepository applicationRepository,
            [FromServices]
            IEnvironmentRepository environmentRepository,
            [FromServices]
            IDeploymentQueue deploymentQueue,
            HttpContext httpContext,
            CancellationToken cancellationToken)
    {
        var application = await applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);

        if (application is null)
        {
            return TypedResults.BadRequest($"Application with Id: {request.ApplicationId} does not exist.");
        }

        var environment = await environmentRepository.GetByIdAsync(request.EnvironmentId, cancellationToken);

        if (environment is null)
        {
            return TypedResults.BadRequest($"Environment with Id: {request.EnvironmentId} does not exist.");
        }

        var deployment = Orchitect.Domain.Engine.Deployment.Deployment.Create(request);
        var deploymentResponse = await repository.CreateAsync(deployment, cancellationToken);

        if (deploymentResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        var deploymentRequest = new DeploymentQueueRequest(application.Id, deploymentResponse.Id);
        await deploymentQueue.QueueDeploymentTaskAsync(deploymentRequest, cancellationToken);

        var locationUrl =
            new Uri(
                $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/deployments/{deploymentResponse.Id.Value}");

        var response = new CreateDeploymentResponse(
            Id: deploymentResponse.Id.Value,
            Status: deploymentResponse.Status.ToString(),
            Location: locationUrl
        );

        return TypedResults.Accepted(locationUrl, response);
    }
}