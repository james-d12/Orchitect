using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Api.Endpoints.Internal;

public sealed class GetRunDescriptorEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet(RunnerRoutes.Descriptor, HandleAsync)
        .WithSummary("Gets what a runner should execute for its run.");

    private static async Task<Results<Ok<RunDescriptor>, NotFound>> HandleAsync(
        [FromRoute]
        Guid runId,
        [FromServices]
        IDeploymentRunRepository runRepository,
        [FromServices]
        IDeploymentRepository deploymentRepository,
        [FromServices]
        IApplicationRepository applicationRepository,
        CancellationToken cancellationToken)
    {
        var run = await runRepository.GetByIdAsync(new DeploymentRunId(runId), cancellationToken);

        if (run is null)
        {
            return TypedResults.NotFound();
        }

        var deployment = await deploymentRepository.GetByIdAsync(run.DeploymentId, cancellationToken);

        if (deployment is null)
        {
            return TypedResults.NotFound();
        }

        var application = await applicationRepository.GetByIdAsync(deployment.ApplicationId, cancellationToken);

        if (application is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new RunDescriptor(
            run.Id.Value,
            ToRunnerOperation(run.Operation),
            application.Repository.Url,
            deployment.CommitId.Value,
            deployment.ApplicationId.Value,
            deployment.EnvironmentId.Value));
    }

    private static RunnerOperation ToRunnerOperation(DeploymentRunOperation operation) => operation switch
    {
        DeploymentRunOperation.Provision => RunnerOperation.Provision,
        DeploymentRunOperation.Destroy => RunnerOperation.Destroy,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };
}
