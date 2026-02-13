namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record OAuthPayload
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string RefreshToken { get; init; }
    public required string TokenUrl { get; init; }
}
