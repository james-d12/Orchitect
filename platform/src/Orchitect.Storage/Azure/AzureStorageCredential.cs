using Azure.Core;
using Azure.Identity;

namespace Orchitect.Storage.Azure;

internal static class AzureStorageCredential
{
    public static TokenCredential Create(AzureBlobStorageOptions options) => options.AccessToken switch
    {
        { Length: > 0 } token => DelegatedTokenCredential.Create((_, _) =>
            new AccessToken(token, options.AccessTokenExpiresOn ?? DateTimeOffset.MaxValue)),
        _ => new DefaultAzureCredential()
    };
}
