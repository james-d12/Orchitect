namespace Orchitect.Storage;

public sealed record StorageKey
{
    private const char Separator = '/';

    public string Value { get; }

    private StorageKey(string value)
    {
        Value = value;
    }

    public static StorageKey Create(string value)
    {
        if (GetValidationError(value) is { } error)
        {
            throw new ArgumentException(error, nameof(value));
        }

        return new StorageKey(value);
    }

    public StorageKey Combine(params string[] segments) => Create(string.Join(Separator, [Value, .. segments]));

    public override string ToString() => Value;

    private static string? GetValidationError(string? value) => value switch
    {
        null or { Length: 0 } => "A storage key must not be empty.",
        _ when value.Any(char.IsControl) => $"Storage key '{value}' must not contain control characters.",
        _ when value.Contains('\\') => $"Storage key '{value}' must use '/' as its separator.",
        _ when value.Split(Separator).Any(segment => segment is "" or "." or "..") =>
            $"Storage key '{value}' must be a relative path without empty, '.' or '..' segments.",
        _ => null
    };
}
