using Orchitect.Infrastructure.Engine.Secret.Azure;

namespace Orchitect.Infrastructure.Engine.Secret;

public enum SecretProviderType
{
    Environment,
    AzureKeyVault
}

public sealed record SecretProviderOptions
{
    public const string SectionName = "SecretProvider";

    public SecretProviderType Type { get; init; } = SecretProviderType.Environment;

    public AzureKeyVaultOptions AzureKeyVault { get; init; } = new();

    public Dictionary<string, string> Mappings { get; init; } = [];

    public string? GetValidationError() => Type switch
    {
        SecretProviderType.Environment => null,
        SecretProviderType.AzureKeyVault => AzureKeyVault.GetValidationError(),
        _ => $"{SectionName}:Type '{Type}' is not supported."
    };
}
