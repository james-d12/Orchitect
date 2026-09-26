using Azure.Core;
using Orchitect.Infrastructure.Engine.Secret;
using Orchitect.Infrastructure.Engine.Secret.Azure;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Secret;

public sealed class KeyVaultRunnerTokenProviderTests
{
    private static readonly DateTimeOffset ExpiresOn = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetEnvironmentAsync_EnvironmentProvider_ReturnsNothing()
    {
        var credential = new FakeCredential();
        var provider = new KeyVaultRunnerTokenProvider(credential);

        var environment = await provider.GetEnvironmentAsync(new SecretProviderOptions
        {
            Type = SecretProviderType.Environment
        });

        Assert.Empty(environment);
        Assert.Null(credential.RequestedScopes);
    }

    [Fact]
    public async Task GetEnvironmentAsync_AzureKeyVault_ReturnsKeyVaultScopedToken()
    {
        var credential = new FakeCredential();
        var provider = new KeyVaultRunnerTokenProvider(credential);

        var environment = await provider.GetEnvironmentAsync(new SecretProviderOptions
        {
            Type = SecretProviderType.AzureKeyVault,
            AzureKeyVault = new AzureKeyVaultOptions { VaultUri = new Uri("https://orchitect.vault.azure.net/") }
        });

        Assert.Equal(["https://vault.azure.net/.default"], credential.RequestedScopes!);
        Assert.Equal("kv-token", environment["SecretProvider__AzureKeyVault__AccessToken"]);
        Assert.Equal(ExpiresOn,
            DateTimeOffset.Parse(environment["SecretProvider__AzureKeyVault__AccessTokenExpiresOn"]));
    }

    private sealed class FakeCredential : TokenCredential
    {
        public string[]? RequestedScopes { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            RequestedScopes = requestContext.Scopes;
            return new AccessToken("kv-token", ExpiresOn);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
