namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record ServicePrincipalPayload
{
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}
