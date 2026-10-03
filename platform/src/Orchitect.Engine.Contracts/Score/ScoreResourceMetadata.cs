namespace Orchitect.Engine.Contracts.Score;

public sealed record ScoreResourceMetadata
{
    public Dictionary<string, string>? Annotations { get; init; }
}