namespace Conductor.Engine.Domain.Requirement;

public sealed record RequirementResult
{
    public required Requirement? Requirement { get; init; }
}