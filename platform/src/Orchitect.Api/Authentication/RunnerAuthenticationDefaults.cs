using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Api.Authentication;

public static class RunnerAuthenticationDefaults
{
    public const string AuthenticationScheme = "Runner";
    public const string PolicyName = "Runner";
    public const string RunIdClaim = "orchitect_run_id";
    public const string RunIdRouteValue = RunnerRoutes.RunIdParameter;
}
