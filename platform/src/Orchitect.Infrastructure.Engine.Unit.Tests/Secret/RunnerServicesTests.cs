using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Infrastructure.Engine.Secret;
using Orchitect.Infrastructure.Engine.Secret.Azure;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Secret;

public sealed class RunnerServicesTests
{
    private const string VaultUri = "https://orchitect.vault.azure.net/";

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Theory]
    [InlineData("HashicorpVault")]
    [InlineData("None")]
    [InlineData("azure-keyvault")]
    [InlineData("")]
    [InlineData("7")]
    public void AddRunnerServices_UnknownSecretProvider_Throws(string type)
    {
        var configuration = Configuration(new() { ["SecretProvider:Type"] = type });

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddRunnerServices(configuration));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("orchitect.vault.azure.net")]
    public void AddRunnerServices_AzureKeyVaultWithoutAbsoluteVaultUri_Throws(string? vaultUri)
    {
        var configuration = Configuration(new()
        {
            ["SecretProvider:Type"] = "AzureKeyVault",
            ["SecretProvider:AzureKeyVault:VaultUri"] = vaultUri
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddRunnerServices(configuration));

        Assert.Contains("VaultUri", exception.Message);
    }

    [Theory]
    [InlineData(null, typeof(EnvironmentSecretProvider))]
    [InlineData("Environment", typeof(EnvironmentSecretProvider))]
    [InlineData("AzureKeyVault", typeof(AzureKeyVaultSecretProvider))]
    [InlineData("azurekeyvault", typeof(AzureKeyVaultSecretProvider))]
    public void AddRunnerServices_RegistersConfiguredSecretProvider(string? type, Type expected)
    {
        var configuration = Configuration(new()
        {
            ["SecretProvider:Type"] = type,
            ["SecretProvider:AzureKeyVault:VaultUri"] = VaultUri
        });

        using var provider = new ServiceCollection().AddLogging().AddRunnerServices(configuration)
            .BuildServiceProvider();

        Assert.IsType(expected, provider.GetRequiredService<ISecretProvider>());
        Assert.NotNull((object?)provider.GetRequiredService<ISecretEnvironmentLoader>());
    }
}
