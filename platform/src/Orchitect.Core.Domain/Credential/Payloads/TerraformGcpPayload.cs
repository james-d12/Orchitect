namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record TerraformGcpPayload
{
    public required string CredentialsJson { get; init; }
    public required string Project { get; init; }
    public required string Region { get; init; }
}
