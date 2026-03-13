using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Services;

public sealed class AzureDevOpsConnectionService : IAzureDevOpsConnectionService
{
    private readonly VssConnection _connection;

    // Constructor for credential-based approach
    public AzureDevOpsConnectionService(string organization, string personalAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(personalAccessToken);

        var connectionUri = new Uri($"https://dev.azure.com/{organization}");
        var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
        _connection = new VssConnection(connectionUri, credentials);
    }

    public Task<T> GetClientAsync<T>(CancellationToken cancellationToken) where T : IVssHttpClient
    {
        return _connection.GetClientAsync<T>(cancellationToken);
    }

    /// <summary>
    /// Factory method for creating connection from Core credential
    /// </summary>
    public static AzureDevOpsConnectionService FromCredential(
        Credential credential,
        CredentialPayloadResolver resolver,
        Dictionary<string, string>? platformConfig = null)
    {
        if (credential.Platform != CredentialPlatform.AzureDevOps)
            throw new InvalidOperationException(
                $"Credential '{credential.Name}' is for {credential.Platform}, expected AzureDevOps");

        var payload = resolver.ResolvePersonalAccessToken(credential);

        if (platformConfig == null || !platformConfig.TryGetValue("organization", out var organization) || string.IsNullOrWhiteSpace(organization))
        {
            throw new InvalidOperationException("AzureDevOps requires 'organization' in platformConfig");
        }

        return new AzureDevOpsConnectionService(organization, payload.Token);
    }
}