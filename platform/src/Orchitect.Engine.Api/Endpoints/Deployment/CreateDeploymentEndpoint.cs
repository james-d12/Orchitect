using Orchitect.Engine.Domain.Application;
using Orchitect.Engine.Domain.Deployment;
using Orchitect.Engine.Domain.Environment;
using Orchitect.Engine.Infrastructure.Resources;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orchitect.Engine.Api.Common;
using Orchitect.Engine.Api.Queue;

namespace Orchitect.Engine.Api.Endpoints.Deployment;

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
            IResourceProvisioner resourceProvisioner,
            [FromServices]
            IBackgroundTaskQueueProcessor backgroundTaskQueueProcessor,
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

        var deployment = Orchitect.Engine.Domain.Deployment.Deployment.Create(request);
        var deploymentResponse = await repository.CreateAsync(deployment, cancellationToken);

        if (deploymentResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        // Capture IDs to pass to background work item (avoiding captured scoped dependencies)
        var deploymentId = deploymentResponse.Id;
        var applicationId = application.Id;

        await backgroundTaskQueueProcessor.QueueBackgroundWorkItemAsync(async (sp, ct) =>
        {
            // Resolve scoped dependencies within the background service scope
            var deploymentRepo = sp.GetRequiredService<IDeploymentRepository>();
            var appRepo = sp.GetRequiredService<IApplicationRepository>();
            var provisioner = sp.GetRequiredService<IResourceProvisioner>();

            var deployment = await deploymentRepo.GetByIdAsync(deploymentId, ct);
            var app = await appRepo.GetByIdAsync(applicationId, ct);

            if (deployment is null || app is null)
            {
                throw new InvalidOperationException(
                    $"Deployment {deploymentId.Value} or Application {applicationId.Value} not found");
            }

            await provisioner.StartAsync(app, deployment, ct);
        });

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