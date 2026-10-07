using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Contracts.Secret;
using Orchitect.Engine.Contracts.Secret.Azure;
using Orchitect.Engine.Contracts.Terraform;
using Orchitect.Engine.Dispatch.Executor;
using Orchitect.Engine.Execution;
using Orchitect.Engine.Execution.Secret;
using Orchitect.Engine.Execution.Secret.Azure;
using Orchitect.Engine.Execution.Artifact;
using Orchitect.Storage;
using Orchitect.Storage.Azure;
using Orchitect.Storage.FileSystem;

namespace Orchitect.Engine.Dispatch.Unit.Tests.Executor;

public sealed class ExecutorRunnerContractTests
{
    private const string VaultUri = "https://orchitect.vault.azure.net/";

    [Fact]
    public void ToEnvironment_RemoteBackendAndKeyVault_BindsBackIntoRunnerOptions()
    {
        var options = new ExecutorOptions
        {
            Image = "runner:test",
            LogLevel = LogLevel.Warning,
            TerraformBackend = new TerraformBackendOptions
            {
                Mode = TerraformBackendMode.Remote,
                Type = "azurerm",
                Config = new Dictionary<string, string>
                {
                    ["resource_group_name"] = "orchitect-state",
                    ["use_azuread_auth"] = "true"
                }
            },
            SecretProvider = new SecretProviderOptions
            {
                Type = SecretProviderType.AzureKeyVault,
                AzureKeyVault = new AzureKeyVaultOptions { VaultUri = new Uri(VaultUri) },
                Mappings = new Dictionary<string, string> { ["ARM_CLIENT_ID"] = "terraform-client-id" }
            }
        };
        var expiresOn = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        var tokenEnvironment = new Dictionary<string, string>
        {
            [RunnerEnvironment.KeyVaultAccessToken] = "kv-token",
            [RunnerEnvironment.KeyVaultAccessTokenExpiresOn] = expiresOn.ToString("O")
        };

        using var provider = BuildRunner(options.ToEnvironment().Concat(tokenEnvironment));

        var backend = provider.GetRequiredService<IOptions<TerraformBackendOptions>>().Value;
        Assert.Equal(TerraformBackendMode.Remote, backend.Mode);
        Assert.Equal("azurerm", backend.Type);
        Assert.Equal(options.TerraformBackend.Config, backend.Config);

        var secretProvider = provider.GetRequiredService<IOptions<SecretProviderOptions>>().Value;
        Assert.Equal(SecretProviderType.AzureKeyVault, secretProvider.Type);
        Assert.Equal(new Uri(VaultUri), secretProvider.AzureKeyVault.VaultUri);
        Assert.Equal("kv-token", secretProvider.AzureKeyVault.AccessToken);
        Assert.Equal(expiresOn, secretProvider.AzureKeyVault.AccessTokenExpiresOn);
        Assert.Equal(options.SecretProvider.Mappings, secretProvider.Mappings);
        Assert.IsType<AzureKeyVaultSecretProvider>(provider.GetRequiredService<ISecretProvider>());

        var configuration = provider.GetRequiredService<IConfiguration>();
        Assert.Equal(nameof(LogLevel.Warning), configuration["Logging:LogLevel:Default"]);
    }

    [Fact]
    public void ToEnvironment_Defaults_BindBackIntoRunnerDefaults()
    {
        using var provider = BuildRunner(new ExecutorOptions { Image = "runner:test" }.ToEnvironment());

        Assert.Equal(new TerraformBackendOptions().Mode,
            provider.GetRequiredService<IOptions<TerraformBackendOptions>>().Value.Mode);
        Assert.IsType<EnvironmentSecretProvider>(provider.GetRequiredService<ISecretProvider>());
        Assert.Same(NullRunArtifactStore.Instance, provider.GetRequiredService<IRunArtifactStore>());
    }

    [Fact]
    public void ToEnvironment_AzureBlobStorage_BindsBackIntoRunnerOptions()
    {
        var options = new ExecutorOptions
        {
            Image = "runner:test",
            Storage = new StorageOptions
            {
                Type = StorageProviderType.AzureBlob,
                AzureBlob = new AzureBlobStorageOptions
                {
                    ContainerUri = new Uri("https://orchitect.blob.core.windows.net/artifacts")
                }
            }
        };
        var expiresOn = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        var tokenEnvironment = new Dictionary<string, string>
        {
            [AzureBlobStorageOptions.AccessTokenEnvironmentKey] = "blob-token",
            [AzureBlobStorageOptions.AccessTokenExpiresOnEnvironmentKey] = expiresOn.ToString("O"),
            [RunnerEnvironment.RunId] = Guid.NewGuid().ToString()
        };

        using var provider = BuildRunner(options.ToEnvironment().Concat(tokenEnvironment));

        var storage = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
        Assert.Equal(StorageProviderType.AzureBlob, storage.Type);
        Assert.Equal(options.Storage.AzureBlob.ContainerUri, storage.AzureBlob.ContainerUri);
        Assert.Equal("blob-token", storage.AzureBlob.AccessToken);
        Assert.Equal(expiresOn, storage.AzureBlob.AccessTokenExpiresOn);
        Assert.IsType<AzureBlobStorageProvider>(provider.GetRequiredService<IStorageProvider>());
        Assert.IsType<RunArtifactStore>(provider.GetRequiredService<IRunArtifactStore>());
    }

    [Fact]
    public void ToEnvironment_FileSystemStorage_BindsBackIntoRunnerOptions()
    {
        var options = new ExecutorOptions
        {
            Image = "runner:test",
            Storage = new StorageOptions
            {
                Type = StorageProviderType.FileSystem,
                FileSystem = new FileSystemStorageOptions { RootPath = "/var/orchitect/artifacts" }
            }
        };

        using var provider = BuildRunner(options.ToEnvironment());

        Assert.Equal("/var/orchitect/artifacts",
            provider.GetRequiredService<IOptions<StorageOptions>>().Value.FileSystem.RootPath);
        Assert.IsType<FileSystemStorageProvider>(provider.GetRequiredService<IStorageProvider>());
    }

    private static ServiceProvider BuildRunner(IEnumerable<KeyValuePair<string, string>> environment)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(environment.Select(kvp =>
                new KeyValuePair<string, string?>(kvp.Key.Replace("__", ConfigurationPath.KeyDelimiter), kvp.Value)))
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddEngineProvisioningServices()
            .AddRunnerServices(configuration)
            .BuildServiceProvider();
    }
}
