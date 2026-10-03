using System.Text.Json;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner.Api;

namespace Orchitect.Engine.Dispatch.Plan;

internal static class StoredRunPlan
{
    public static string Serialize(RunPlan plan) => JsonSerializer.Serialize(plan, RunnerContract.JsonOptions);

    public static RunPlan Deserialize(DeploymentRunPlan stored) =>
        JsonSerializer.Deserialize<RunPlan>(stored.Contents, RunnerContract.JsonOptions)
        ?? throw new InvalidOperationException($"The stored plan of run '{stored.RunId.Value}' is empty.");
}
