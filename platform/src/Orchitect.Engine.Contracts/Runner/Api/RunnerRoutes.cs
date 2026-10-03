namespace Orchitect.Engine.Contracts.Runner.Api;

public static class RunnerRoutes
{
    public const string RunIdParameter = "runId";
    public const string Run = $"/internal/runs/{{{RunIdParameter}:guid}}";
    public const string Descriptor = "/";
    public const string Plan = "/plan";
    public const string Complete = "/complete";

    public static string ForRun(Guid runId, string route = Descriptor) =>
        $"internal/runs/{runId:D}{route.TrimEnd('/')}";
}
