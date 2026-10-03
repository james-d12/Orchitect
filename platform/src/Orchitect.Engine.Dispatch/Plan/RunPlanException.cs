namespace Orchitect.Engine.Dispatch.Plan;

public sealed class RunPlanException(RunPlanFailure failure, string message) : Exception(message)
{
    public RunPlanFailure Failure { get; } = failure;
}
