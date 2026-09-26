using Azure.Core;

namespace Orchitect.Infrastructure.Engine.Secret.Azure;

public sealed class KeyVaultRunnerTokenProvider : IRunnerSecretTokenProvider
{
    private const string KeyVaultScope = "https://vault.azure.net/.default";
    private const string Prefix = $"{SecretProviderOptions.SectionName}__AzureKeyVault__";

    public const string AccessTokenKey = $"{Prefix}{nameof(AzureKeyVaultOptions.AccessToken)}";
    public const string AccessTokenExpiresOnKey = $"{Prefix}{nameof(AzureKeyVaultOptions.AccessTokenExpiresOn)}";

    private readonly TokenCredential _credential;

    public KeyVaultRunnerTokenProvider(TokenCredential credential)
    {
        _credential = credential;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetEnvironmentAsync(SecretProviderOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Type != SecretProviderType.AzureKeyVault)
        {
            return new Dictionary<string, string>();
        }

        var token = await _credential.GetTokenAsync(new TokenRequestContext([KeyVaultScope]), cancellationToken);

        return new Dictionary<string, string>
        {
            [AccessTokenKey] = token.Token,
            [AccessTokenExpiresOnKey] = token.ExpiresOn.ToString("O")
        };
    }
}
