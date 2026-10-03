namespace Orchitect.Engine.Contracts.Score;

public sealed record ScoreFile
{
    public required string ApiVersion { get; init; }
    public required ScoreMetadata Metadata { get; init; }
    public Dictionary<string, ScoreResource>? Resources { get; init; }
}