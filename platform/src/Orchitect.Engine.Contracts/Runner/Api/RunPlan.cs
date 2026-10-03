namespace Orchitect.Engine.Contracts.Runner.Api;

public sealed record RunPlan(RunContext Context, IReadOnlyList<RunInput> Inputs);
