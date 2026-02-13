namespace Orchitect.Core.Domain.Credential.Payloads;

public sealed record TerraformAwsPayload
{
    public required string AccessKeyId { get; init; }
    public required string SecretAccessKey { get; init; }
    public string SessionToken { get; init; } = string.Empty;
    public required string Region { get; init; }
}
