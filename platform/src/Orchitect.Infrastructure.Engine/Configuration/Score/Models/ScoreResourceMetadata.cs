namespace Orchitect.Infrastructure.Engine.Configuration.Score.Models;

public sealed record ScoreResourceMetadata
{
    public Dictionary<string, string>? Annotations { get; init; }
}