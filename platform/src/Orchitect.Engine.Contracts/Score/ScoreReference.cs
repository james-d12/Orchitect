using System.Text.RegularExpressions;

namespace Orchitect.Engine.Contracts.Score;

public sealed partial record ScoreReference(string Key, string Output, int Index, int Length)
{
    private const string Prefix = "${resources.";

    public static IReadOnlyList<ScoreReference> Find(string value) =>
        ReferenceRegex().Matches(value)
            .Select(m => new ScoreReference(m.Groups["key"].Value, m.Groups["output"].Value, m.Index, m.Length))
            .ToList();

    public static bool HasMalformed(string value) =>
        CountPrefixes(value) != ReferenceRegex().Count(value);

    private static int CountPrefixes(string value)
    {
        var count = 0;

        for (var index = value.IndexOf(Prefix, StringComparison.Ordinal);
             index >= 0;
             index = value.IndexOf(Prefix, index + Prefix.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [GeneratedRegex(@"\$\{resources\.(?<key>[A-Za-z0-9_-]+)\.(?<output>[A-Za-z_][A-Za-z0-9_-]*)\}")]
    private static partial Regex ReferenceRegex();
}
