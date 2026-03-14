namespace Orchitect.Domain.Engine.Requirement;

public record Requirement
{
    public required string Name { get; init; }
    public required List<RequirementResource> Resources { get; init; }
}