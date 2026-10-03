using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Authentication;

namespace Orchitect.Api.Endpoints.Internal;

public static class RunnerEndpoints
{
    public static RouteGroupBuilder MapRunnerGroup(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGroup($"/internal/runs/{{{RunnerAuthenticationDefaults.RunIdRouteValue}:guid}}")
            .RequireAuthorization(RunnerAuthenticationDefaults.PolicyName)
            .ExcludeFromDescription();
    }
}
