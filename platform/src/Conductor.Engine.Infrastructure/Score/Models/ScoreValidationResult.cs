namespace Conductor.Engine.Infrastructure.Score.Models;

public sealed record ScoreValidationResult
{
    public required string ScoreFilePath { get; init; }
    public required ScoreValidationResultState State { get; init; }

    public static ScoreValidationResult Valid(string scoreFilePath) => new()
    {
        ScoreFilePath = scoreFilePath,
        State = ScoreValidationResultState.Valid
    };

    public static ScoreValidationResult ScoreFileNotFound() => new()
    {
        ScoreFilePath = string.Empty,
        State = ScoreValidationResultState.FileNotFound
    };

    public static ScoreValidationResult CloneFailed() => new()
    {
        ScoreFilePath = string.Empty,
        State = ScoreValidationResultState.CloneFailed
    };
}