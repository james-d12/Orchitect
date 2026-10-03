using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Plan;

namespace Orchitect.Api.Endpoints.Internal;

public sealed class CreateRunPlanEndpoint : IEndpoint
{
    public const string InvalidErrorCode = "RunPlanInvalid";
    public const string ConflictErrorCode = "RunNotRunning";

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost(RunnerRoutes.Plan, HandleAsync)
        .WithSummary("Records the run's resources from its score file and returns the plan the runner executes.");

    private static async Task<Results<Ok<RunPlan>, NotFound, BadRequest<ErrorResponse>, Conflict<ErrorResponse>>>
        HandleAsync(
            [FromRoute]
            Guid runId,
            [FromBody]
            ScoreSubmission submission,
            [FromServices]
            IRunPlanner runPlanner,
            CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(
                await runPlanner.PlanAsync(new DeploymentRunId(runId), submission.ScoreFile, cancellationToken));
        }
        catch (RunPlanException exception)
        {
            return exception.Failure switch
            {
                RunPlanFailure.RunNotFound => TypedResults.NotFound(),
                RunPlanFailure.RunNotRunning => TypedResults.Conflict(Errors(ConflictErrorCode, exception.Message)),
                _ => TypedResults.BadRequest(Errors(InvalidErrorCode, exception.Message))
            };
        }
    }

    private static ErrorResponse Errors(string code, string message) =>
        new() { Errors = [new Error { Code = code, Message = message }] };
}
