using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Dispatch.Completion;

namespace Orchitect.Api.Endpoints.Internal;

public sealed class CompleteRunEndpoint : IEndpoint
{
    public const string InvalidOutcomeErrorCode = "InvalidRunOutcome";

    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost(RunnerRoutes.Complete, HandleAsync)
        .WithSummary("Records the outcome a runner reports for its run.");

    private static async Task<Results<NoContent, BadRequest<ErrorResponse>>> HandleAsync(
        [FromRoute]
        Guid runId,
        [FromBody]
        RunCompletion completion,
        [FromServices]
        IRunCompletionHandler completionHandler,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(completion.Outcome))
        {
            return TypedResults.BadRequest(new ErrorResponse
            {
                Errors =
                [
                    new Error
                    {
                        Code = InvalidOutcomeErrorCode,
                        Message = $"The outcome must be {RunOutcome.Succeeded} or {RunOutcome.Failed}."
                    }
                ]
            });
        }

        await completionHandler.CompleteAsync(new DeploymentRunId(runId), RunResult.FromReport(completion),
            CancellationToken.None);

        return TypedResults.NoContent();
    }
}
