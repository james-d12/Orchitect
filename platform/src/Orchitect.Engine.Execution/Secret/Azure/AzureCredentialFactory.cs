using Azure.Core;
using Azure.Identity;
using Orchitect.Engine.Contracts.Secret.Azure;

namespace Orchitect.Engine.Execution.Secret.Azure;

internal static class AzureCredentialFactory
{
    public static TokenCredential Create(AzureKeyVaultOptions options) => options.AccessToken switch
    {
        { Length: > 0 } token => DelegatedTokenCredential.Create((_, _) =>
            new AccessToken(token, options.AccessTokenExpiresOn ?? DateTimeOffset.MaxValue)),
        _ => new DefaultAzureCredential()
    };
}
