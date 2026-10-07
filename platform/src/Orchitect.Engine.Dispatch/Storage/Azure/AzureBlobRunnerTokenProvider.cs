using Azure.Core;
using Orchitect.Storage;
using Orchitect.Storage.Azure;

namespace Orchitect.Engine.Dispatch.Storage.Azure;

public sealed class AzureBlobRunnerTokenProvider : IRunnerStorageTokenProvider
{
    private const string StorageScope = "https://storage.azure.com/.default";

    private readonly TokenCredential _credential;

    public AzureBlobRunnerTokenProvider(TokenCredential credential)
    {
        _credential = credential;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetEnvironmentAsync(StorageOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Type != StorageProviderType.AzureBlob)
        {
            return new Dictionary<string, string>();
        }

        var token = await _credential.GetTokenAsync(new TokenRequestContext([StorageScope]), cancellationToken);

        return new Dictionary<string, string>
        {
            [AzureBlobStorageOptions.AccessTokenEnvironmentKey] = token.Token,
            [AzureBlobStorageOptions.AccessTokenExpiresOnEnvironmentKey] = token.ExpiresOn.ToString("O")
        };
    }
}
