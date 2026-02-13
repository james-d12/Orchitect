namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record PersonalAccessTokenPayload
{
    public required string Token { get; init; }
}
