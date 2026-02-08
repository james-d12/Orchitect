namespace Conductor.Engine.Infrastructure.Score.Models;

public sealed record ScoreResource
{
    public string Type { get; init; } = null!;
    public string? Class { get; init; }
    public string? Id { get; init; }
    public ScoreResourceMetadata? Metadata { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
}