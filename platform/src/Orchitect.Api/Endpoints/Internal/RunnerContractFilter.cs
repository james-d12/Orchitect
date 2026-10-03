using System.Globalization;
using Microsoft.AspNetCore.Http;
using Orchitect.Api.Shared;
using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Api.Endpoints.Internal;

public sealed class RunnerContractFilter : IEndpointFilter
{
    public const string MismatchErrorCode = "RunnerContractMismatch";

    private static readonly string ExpectedVersion =
        RunnerContract.Version.ToString(CultureInfo.InvariantCulture);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var versions = context.HttpContext.Request.Headers[RunnerContract.HeaderName];

        if (versions.Count == 1 && versions[0] == ExpectedVersion)
        {
            return await next(context);
        }

        var received = versions.Count == 0 ? "none" : $"'{versions}'";

        return TypedResults.BadRequest(new ErrorResponse
        {
            Errors =
            [
                new Error
                {
                    Code = MismatchErrorCode,
                    Message = $"The {RunnerContract.HeaderName} header must be '{ExpectedVersion}' but was " +
                              $"{received}. The runner image and the API were built from different contract versions."
                }
            ]
        });
    }
}
