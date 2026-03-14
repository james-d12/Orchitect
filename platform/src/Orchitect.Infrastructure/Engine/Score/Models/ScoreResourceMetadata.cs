namespace Orchitect.Infrastructure.Engine.Score.Models;

public sealed record ScoreResourceMetadata
{
    public Dictionary<string, string>? Annotations { get; init; }
}