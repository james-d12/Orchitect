using Orchitect.Infrastructure.Engine.Executor;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Secret;
using Orchitect.Infrastructure.Engine.Secret.Azure;

namespace Orchitect.Infrastructure.Engine.Unit.Tests.Executor;

public sealed class ExecutorOptionsTests
{
    private const string ClientSecret = "super-secret-value";

    private static ExecutorOptions CreateOptions() => new()
    {
        Image = "orchitect-runner:azure-terraform",
        TerraformBackend = new TerraformBackendOptions
        {
            Mode = TerraformBackendMode.Remote,
            Type = "azurerm",
            Config = new Dictionary<string, string> { ["key"] = "{applicationId}/{environmentId}.tfstate" }
        },
        SecretProvider = new SecretProviderOptions
        {
            Type = SecretProviderType.AzureKeyVault,
            AzureKeyVault = new AzureKeyVaultOptions { VaultUri = new Uri("https://orchitect.vault.azure.net/") },
            Mappings = new Dictionary<string, string> { ["ARM_CLIENT_SECRET"] = "arm-client-secret" }
        },
        Configuration = new Dictionary<string, string> { ["AZURE_CLIENT_SECRET"] = ClientSecret }
    };

    [Fact]
    public void ToEnvironment_FlattensSectionsIntoRunnerConfigurationKeys()
    {
        var environment = CreateOptions().ToEnvironment();

        Assert.Equal("Remote", environment["TerraformBackend__Mode"]);
        Assert.Equal("azurerm", environment["TerraformBackend__Type"]);
        Assert.Equal("{applicationId}/{environmentId}.tfstate", environment["TerraformBackend__Config__key"]);
        Assert.Equal("AzureKeyVault", environment["SecretProvider__Type"]);
        Assert.Equal("https://orchitect.vault.azure.net/", environment["SecretProvider__AzureKeyVault__VaultUri"]);
        Assert.Equal("arm-client-secret", environment["SecretProvider__Mappings__ARM_CLIENT_SECRET"]);
        Assert.Equal(ClientSecret, environment["AZURE_CLIENT_SECRET"]);
    }

    [Fact]
    public void ToEnvironment_LocalBackend_OnlySetsMode()
    {
        var options = CreateOptions() with { TerraformBackend = new TerraformBackendOptions() };

        var backendKeys = options.ToEnvironment().Where(kvp => kvp.Key.StartsWith("TerraformBackend__")).ToList();

        Assert.Equal([new KeyValuePair<string, string>("TerraformBackend__Mode", "Local")], backendKeys);
    }

    [Fact]
    public void ToString_DoesNotExposeConfigurationValues()
    {
        var options = CreateOptions();
        var context = new ExecutorContext
        {
            Image = options.Image,
            RunId = "run",
            Arguments = [],
            Configuration = options.ToEnvironment()
        };

        Assert.DoesNotContain(ClientSecret, options.ToString());
        Assert.DoesNotContain(ClientSecret, context.ToString());
    }
}
