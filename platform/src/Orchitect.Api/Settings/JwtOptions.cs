using System.Text;

namespace Orchitect.Api.Settings;

public sealed record JwtOptions
{
    public const string SectionName = "JwtOptions";

    /// <summary>The smallest HS256 signing key allowed, in bytes (256 bits).</summary>
    public const int MinimumSecretBytes = 32;

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int ExpirationInMinutes { get; init; }
    public required string Secret { get; init; }

    /// <summary>Returns whether the secret encodes to at least <see cref="MinimumSecretBytes"/> UTF-8 bytes.</summary>
    public static bool IsSecretLongEnough(string? secret) =>
        secret is not null && Encoding.UTF8.GetByteCount(secret) >= MinimumSecretBytes;
}
