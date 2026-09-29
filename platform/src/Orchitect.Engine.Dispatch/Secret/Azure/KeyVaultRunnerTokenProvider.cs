using Azure.Core;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Secret;

namespace Orchitect.Engine.Dispatch.Secret.Azure;

public sealed class KeyVaultRunnerTokenProvider : IRunnerSecretTokenProvider
{
    private const string KeyVaultScope = "https://vault.azure.net/.default";

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
            [RunnerEnvironment.KeyVaultAccessToken] = token.Token,
            [RunnerEnvironment.KeyVaultAccessTokenExpiresOn] = token.ExpiresOn.ToString("O")
        };
    }
}
