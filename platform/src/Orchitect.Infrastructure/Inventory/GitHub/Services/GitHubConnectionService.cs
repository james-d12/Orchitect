using Octokit;
using Orchitect.Domain.Core.Credential;

namespace Orchitect.Infrastructure.Inventory.GitHub.Services;

public sealed class GitHubConnectionService : IGitHubConnectionService
{
    public GitHubClient Client { get; }

    // Constructor for credential-based approach
    public GitHubConnectionService(string token, string agentName = "Orchitect")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        Client = new GitHubClient(new ProductHeaderValue(agentName))
        {
            Credentials = new Credentials(token)
        };
    }

    /// <summary>
    /// Factory method for creating connection from Core credential
    /// </summary>
    public static GitHubConnectionService FromCredential(
        Credential credential,
        CredentialPayloadResolver resolver,
        Dictionary<string, string>? platformConfig = null)
    {
        if (credential.Platform != CredentialPlatform.GitHub)
            throw new InvalidOperationException(
                $"Credential '{credential.Name}' is for {credential.Platform}, expected GitHub");

        var payload = resolver.ResolvePersonalAccessToken(credential);
        var agentName = platformConfig?.GetValueOrDefault("agentName", "Orchitect") ?? "Orchitect";

        return new GitHubConnectionService(payload.Token, agentName);
    }
}