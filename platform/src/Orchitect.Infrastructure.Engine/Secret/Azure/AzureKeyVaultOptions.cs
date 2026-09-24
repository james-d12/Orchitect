namespace Orchitect.Infrastructure.Engine.Secret.Azure;

public sealed record AzureKeyVaultOptions
{
    private const string ConfigPath = $"{SecretProviderOptions.SectionName}:AzureKeyVault";

    public Uri? VaultUri { get; init; }

    public string? GetValidationError() => VaultUri switch
    {
        null => $"{ConfigPath}:VaultUri is required when {SecretProviderOptions.SectionName}:Type is " +
                $"{SecretProviderType.AzureKeyVault}.",
        { IsAbsoluteUri: false } => $"{ConfigPath}:VaultUri '{VaultUri}' must be an absolute URI.",
        _ => null
    };
}
