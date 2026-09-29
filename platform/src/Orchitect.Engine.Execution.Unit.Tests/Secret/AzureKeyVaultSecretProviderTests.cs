using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchitect.Engine.Execution.Secret.Azure;

namespace Orchitect.Engine.Execution.Unit.Tests.Secret;

public sealed class AzureKeyVaultSecretProviderTests
{
    private const string SecretName = "terraform-client-secret";
    private static readonly Uri VaultUri = new("https://orchitect.vault.azure.net/");

    private readonly SecretClient _client = Substitute.For<SecretClient>();

    public AzureKeyVaultSecretProviderTests()
    {
        _client.VaultUri.Returns(VaultUri);
    }

    [Fact]
    public async Task GetAsync_SecretExists_ReturnsNameAndValue()
    {
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties(SecretName), "s3cr3t");
        _client.GetSecretAsync(SecretName, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(secret, Substitute.For<Response>()));

        var result = await CreateProvider().GetAsync(SecretName);

        Assert.NotNull(result);
        Assert.Equal(SecretName, result.Name);
        Assert.Equal("s3cr3t", result.Value);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        GetSecretThrows(new RequestFailedException(404, "Not found"));

        var result = await CreateProvider().GetAsync(SecretName);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task GetAsync_Unauthorised_ThrowsWithVaultUriAndInner(int status)
    {
        var failure = new RequestFailedException(status, "Denied");
        GetSecretThrows(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateProvider().GetAsync(SecretName));

        Assert.Contains(VaultUri.ToString(), exception.Message);
        Assert.Contains(SecretName, exception.Message);
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task GetAsync_CredentialUnavailable_ThrowsWithVaultUriAndInner()
    {
        var failure = new CredentialUnavailableException("No identity");
        GetSecretThrows(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateProvider().GetAsync(SecretName));

        Assert.Contains(VaultUri.ToString(), exception.Message);
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task GetAsync_AuthenticationFailed_ThrowsWithVaultUriAndInner()
    {
        var failure = new AuthenticationFailedException("Bad credential");
        GetSecretThrows(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateProvider().GetAsync(SecretName));

        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task GetAsync_OtherRequestFailure_Propagates()
    {
        var failure = new RequestFailedException(500, "Server error");
        GetSecretThrows(failure);

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() =>
            CreateProvider().GetAsync(SecretName));

        Assert.Same(failure, exception);
    }

    private void GetSecretThrows(Exception exception) =>
        _client.GetSecretAsync(SecretName, cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);

    private AzureKeyVaultSecretProvider CreateProvider() =>
        new(NullLogger<AzureKeyVaultSecretProvider>.Instance, _client);
}
