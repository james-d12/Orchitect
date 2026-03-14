namespace Orchitect.Domain.Core.Credential.Payloads;

public sealed record PersonalAccessTokenPayload
{
    public required string Token { get; init; }
}
