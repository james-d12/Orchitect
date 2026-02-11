namespace Orchitect.Core.Api.Configuration;

public sealed record JwtOptions
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int ExpirationInMinutes { get; init; }
    public required string Secret { get; init; }
}