using NGitLab;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.GitLab.Services;

public sealed class GitLabConnectionService : IGitLabConnectionService
{
    public GitLabClient Client { get; }

    // Constructor for credential-based approach
    public GitLabConnectionService(string hostUrl, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        Client = new GitLabClient(hostUrl, token);
    }

    /// <summary>
    /// Factory method for creating connection from Core credential
    /// </summary>
    public static GitLabConnectionService FromCredential(
        Credential credential,
        CredentialPayloadResolver resolver,
        Dictionary<string, string>? platformConfig = null)
    {
        if (credential.Platform != CredentialPlatform.GitLab)
            throw new InvalidOperationException(
                $"Credential '{credential.Name}' is for {credential.Platform}, expected GitLab");

        var payload = resolver.ResolvePersonalAccessToken(credential);
        var hostUrl = platformConfig?.GetValueOrDefault("hostUrl", "https://gitlab.com")
            ?? "https://gitlab.com";

        return new GitLabConnectionService(hostUrl, payload.Token);
    }
}