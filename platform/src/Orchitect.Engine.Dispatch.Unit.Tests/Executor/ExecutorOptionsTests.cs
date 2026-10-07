using Microsoft.Extensions.Logging;
using Orchitect.Engine.Contracts.Secret;
using Orchitect.Engine.Contracts.Secret.Azure;
using Orchitect.Engine.Contracts.Terraform;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Storage;
using Orchitect.Storage.Azure;
using Orchitect.Storage.FileSystem;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Executor;

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
        Assert.DoesNotContain("AZURE_CLIENT_SECRET", environment.Keys);
        Assert.DoesNotContain(ClientSecret, environment.Values);
    }

    [Fact]
    public void ToEnvironment_NoStorage_OnlySetsType()
    {
        var storageKeys = CreateOptions().ToEnvironment().Where(kvp => kvp.Key.StartsWith("Storage__")).ToList();

        Assert.Equal([new KeyValuePair<string, string>("Storage__Type", "None")], storageKeys);
        Assert.Empty(CreateOptions().ToBinds());
    }

    [Fact]
    public void ToEnvironment_FileSystemStorage_SetsRootPathAndBindsTheHostPath()
    {
        var options = CreateOptions() with
        {
            Storage = new StorageOptions
            {
                Type = StorageProviderType.FileSystem,
                FileSystem = new FileSystemStorageOptions { RootPath = "/var/orchitect/artifacts" }
            },
            StorageHostPath = "/srv/orchitect/artifacts"
        };

        var environment = options.ToEnvironment();

        Assert.Equal("FileSystem", environment["Storage__Type"]);
        Assert.Equal("/var/orchitect/artifacts", environment["Storage__FileSystem__RootPath"]);
        Assert.Equal(["/srv/orchitect/artifacts:/var/orchitect/artifacts"], options.ToBinds());
    }

    [Fact]
    public void ToEnvironment_AzureBlobStorage_SetsContainerUri()
    {
        var options = CreateOptions() with
        {
            Storage = new StorageOptions
            {
                Type = StorageProviderType.AzureBlob,
                AzureBlob = new AzureBlobStorageOptions
                {
                    ContainerUri = new Uri("https://orchitect.blob.core.windows.net/artifacts")
                }
            }
        };

        var environment = options.ToEnvironment();

        Assert.Equal("AzureBlob", environment["Storage__Type"]);
        Assert.Equal("https://orchitect.blob.core.windows.net/artifacts",
            environment["Storage__AzureBlob__ContainerUri"]);
        Assert.DoesNotContain(AzureBlobStorageOptions.AccessTokenEnvironmentKey, environment.Keys);
    }

    [Fact]
    public void ToEnvironment_SetsRunnerLogLevel_DefaultingToInformation()
    {
        Assert.Equal("Information", CreateOptions().ToEnvironment()["Logging__LogLevel__Default"]);
        Assert.Equal("Warning",
            (CreateOptions() with { LogLevel = LogLevel.Warning }).ToEnvironment()["Logging__LogLevel__Default"]);
    }

    [Fact]
    public void Timeout_DefaultsToOneHourAndOutlastsStopGracePeriod()
    {
        var options = CreateOptions();

        Assert.Equal(TimeSpan.FromHours(1), options.Timeout);
        Assert.True(options.Timeout > options.StopGracePeriod);
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
            Configuration = options.ToEnvironment(),
            Secrets = options.Configuration
        };

        Assert.DoesNotContain(ClientSecret, options.ToString());
        Assert.DoesNotContain(ClientSecret, context.ToString());
    }
}
