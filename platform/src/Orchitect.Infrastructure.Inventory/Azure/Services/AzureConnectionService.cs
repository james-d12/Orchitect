using Azure.Identity;
using Azure.ResourceManager;
using Orchitect.Domain.Core.Credential;

namespace Orchitect.Infrastructure.Inventory.Azure.Services;

public sealed class AzureConnectionService : IAzureConnectionService
{
    public ArmClient Client { get; }

    public AzureConnectionService(string tenantId, string clientId, string clientSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        Client = new ArmClient(credential);
    }

    /// <summary>
    /// Factory method for creating connection from Core credential
    /// </summary>
    public static AzureConnectionService FromCredential(
        Credential credential,
        CredentialPayloadResolver resolver,
        Dictionary<string, string>? platformConfig = null)
    {
        if (credential.Platform != CredentialPlatform.Azure)
            throw new InvalidOperationException(
                $"Credential '{credential.Name}' is for {credential.Platform}, expected Azure");

        var payload = resolver.ResolveServicePrincipal(credential);

        return new AzureConnectionService(
            payload.TenantId,
            payload.ClientId,
            payload.ClientSecret);
    }
}
