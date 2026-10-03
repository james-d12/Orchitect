using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Extensions;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Git;
using Orchitect.Engine.Dispatch.Queue;

namespace Orchitect.Api.Endpoints.Engine.Deployment;

public sealed class CreateDeploymentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/", HandleAsync)
        .WithSummary("Creates a new deployment into an environment for an application with a given commit id.");

    private sealed record CreateDeploymentResponse(Guid Id, string Status, Uri Location);

    private static async Task<Results<Accepted<CreateDeploymentResponse>, BadRequest<string>, Conflict<string>, InternalServerError>>
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
            [FromServices]
            IOrganisationRepository organisationRepository,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken)
    {
        if (!GitValidator.IsValidCommitId(request.CommitId.Value))
        {
            return TypedResults.BadRequest($"Commit id '{request.CommitId.Value}' is not a full git commit SHA.");
        }

        var application = await applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);

        if (application is null ||
            !await organisationRepository.IsMemberAsync(user, application.OrganisationId, cancellationToken))
        {
            return TypedResults.BadRequest($"Application with Id: {request.ApplicationId} does not exist.");
        }

        var environment = await environmentRepository.GetByIdAsync(request.EnvironmentId, cancellationToken);

        if (environment is null ||
            !await organisationRepository.IsMemberAsync(user, environment.OrganisationId, cancellationToken))
        {
            return TypedResults.BadRequest($"Environment with Id: {request.EnvironmentId} does not exist.");
        }

        var latest = await repository.GetLatestAsync(request.ApplicationId, request.EnvironmentId, cancellationToken);

        if (latest is { IsActive: true })
        {
            return TypedResults.Conflict(
                $"Deployment with Id: {latest.Id.Value} is still {latest.Status} for this application and environment.");
        }

        var deployment = Orchitect.Domain.Engine.Deployment.Deployment.Create(request, httpContext.User.GetRequestedBy());
        Orchitect.Domain.Engine.Deployment.Deployment? deploymentResponse;

        try
        {
            deploymentResponse = await repository.CreateAsync(deployment, cancellationToken);
        }
        catch (ActiveDeploymentExistsException exception)
        {
            return TypedResults.Conflict(exception.Message);
        }

        if (deploymentResponse is null)
        {
            return TypedResults.InternalServerError();
        }

        var deploymentRequest = new DeploymentQueueRequest(application.Id, deploymentResponse.Id);

        try
        {
            await deploymentQueue.QueueDeploymentTaskAsync(deploymentRequest, cancellationToken);
        }
        catch
        {
            await repository.UpdateAsync(deploymentResponse.Interrupt("The deployment could not be queued."), CancellationToken.None);
            throw;
        }

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