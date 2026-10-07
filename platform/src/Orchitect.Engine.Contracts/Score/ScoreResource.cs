namespace Orchitect.Engine.Contracts.Score;

public sealed record ScoreResource
{
    public const string PreviousKeysAnnotation = "orchitect.io/previous-keys";

    public string Type { get; init; } = null!;
    public string? Class { get; init; }
    public string? Id { get; init; }
    public ScoreResourceMetadata? Metadata { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }

    public IReadOnlyList<string> GetPreviousKeys() =>
        Metadata?.Annotations?.GetValueOrDefault(PreviousKeysAnnotation) is { } value
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList()
            : [];
}
