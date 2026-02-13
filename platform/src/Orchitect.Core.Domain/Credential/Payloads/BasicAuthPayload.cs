namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record BasicAuthPayload
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}
