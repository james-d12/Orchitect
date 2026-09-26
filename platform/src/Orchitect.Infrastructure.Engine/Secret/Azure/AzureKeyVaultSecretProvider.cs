using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace Orchitect.Infrastructure.Engine.Secret.Azure;

public sealed class AzureKeyVaultSecretProvider(ILogger<AzureKeyVaultSecretProvider> logger, SecretClient secretClient)
    : ISecretProvider
{
    public async Task<Secret?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Attempting to retrieve secret: {Name}", name);

        try
        {
            var secret = await secretClient.GetSecretAsync(name, cancellationToken: cancellationToken);
            return new Secret(secret.Value.Name, secret.Value.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (RequestFailedException exception) when (exception.Status is 401 or 403)
        {
            throw new InvalidOperationException(
                $"Key Vault '{secretClient.VaultUri}' rejected the runner's token reading secret '{name}'. " +
                "The access token passed by the API may have expired, or its identity lacks 'Key Vault Secrets User'.",
                exception);
        }
        catch (Exception exception) when (exception is CredentialUnavailableException or AuthenticationFailedException)
        {
            throw new InvalidOperationException(
                $"Runner has no Azure identity to reach Key Vault '{secretClient.VaultUri}'. " +
                "The API did not pass a Key Vault access token and no managed identity is available.",
                exception);
        }
    }
}
