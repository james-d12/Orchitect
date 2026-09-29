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
