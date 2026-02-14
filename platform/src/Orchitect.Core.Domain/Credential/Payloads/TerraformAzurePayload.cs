namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record TerraformAzurePayload
{
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required bool UseMsi { get; init; }
    public string MsiApiVersion { get; init; } = "2019-08-01";
}
