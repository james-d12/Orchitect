using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace Orchitect.Infrastructure.Engine.Secret;

public sealed class AzureKeyVaultSecretProvider(ILogger<AzureKeyVaultSecretProvider> logger, SecretClient secretClient)
    : ISecretProvider
{
    public async Task<Secret?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Attempting to retrieve secret: {Name}", name);
            var secret = await secretClient.GetSecretAsync(name, cancellationToken: cancellationToken);
            return new Secret(secret.Value.Name, secret.Value.Value);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An error occurred while retrieving the secret.");
            return null;
        }
    }
}