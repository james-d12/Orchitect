using Azure.Core;
using Orchitect.Engine.Dispatch.Storage.Azure;
using Orchitect.Storage;
using Orchitect.Storage.Azure;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Storage;

public sealed class AzureBlobRunnerTokenProviderTests
{
    private static readonly DateTimeOffset ExpiresOn = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(StorageProviderType.None)]
    [InlineData(StorageProviderType.FileSystem)]
    public async Task GetEnvironmentAsync_NotAzureBlob_ReturnsNothing(StorageProviderType type)
    {
        var credential = new FakeCredential();
        var provider = new AzureBlobRunnerTokenProvider(credential);

        var environment = await provider.GetEnvironmentAsync(new StorageOptions { Type = type });

        Assert.Empty(environment);
        Assert.Null(credential.RequestedScopes);
    }

    [Fact]
    public async Task GetEnvironmentAsync_AzureBlob_ReturnsStorageScopedToken()
    {
        var credential = new FakeCredential();
        var provider = new AzureBlobRunnerTokenProvider(credential);

        var environment = await provider.GetEnvironmentAsync(new StorageOptions
        {
            Type = StorageProviderType.AzureBlob,
            AzureBlob = new AzureBlobStorageOptions
            {
                ContainerUri = new Uri("https://orchitect.blob.core.windows.net/artifacts")
            }
        });

        Assert.Equal(["https://storage.azure.com/.default"], credential.RequestedScopes!);
        Assert.Equal("blob-token", environment["Storage__AzureBlob__AccessToken"]);
        Assert.Equal(ExpiresOn, DateTimeOffset.Parse(environment["Storage__AzureBlob__AccessTokenExpiresOn"]));
    }

    private sealed class FakeCredential : TokenCredential
    {
        public string[]? RequestedScopes { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            RequestedScopes = requestContext.Scopes;
            return new AccessToken("blob-token", ExpiresOn);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
