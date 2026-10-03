namespace Orchitect.Engine.Contracts.Runner.Api;

public sealed record RunCompletion(RunOutcome Outcome, string? ErrorSummary);
