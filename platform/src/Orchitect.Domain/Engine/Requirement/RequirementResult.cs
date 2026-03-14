namespace Orchitect.Domain.Engine.Requirement;

public sealed record RequirementResult
{
    public required Requirement? Requirement { get; init; }
}