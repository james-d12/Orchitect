using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Authentication;
using Orchitect.Api.Shared;
using Orchitect.Api.Shared.Authorization;
using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Api.Endpoints.Internal;

public static class RunnerEndpoints
{
    public static void MapRunnerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRunnerGroup()
            .MapEndpoint<GetRunDescriptorEndpoint>();
    }

    public static RouteGroupBuilder MapRunnerGroup(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGroup(RunnerRoutes.Run)
            .RequireAuthorization(RunnerAuthenticationDefaults.PolicyName)
            .HandlesOrganisationScope()
            .AddEndpointFilter<RunnerContractFilter>()
            .ExcludeFromDescription();
    }
}
