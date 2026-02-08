namespace Conductor.Engine.Domain.Requirement;

public record Requirement
{
    public required string Name { get; init; }
    public required List<RequirementResource> Resources { get; init; }
}