namespace Orchitect.Engine.Domain.Requirement;

public record RequirementResource()
{
    public required string Id { get; init; }
    public required string Class { get; init; }
    public required string Type { get; init; } = null!;
    public required Dictionary<string, string>? Parameters { get; init; }
}