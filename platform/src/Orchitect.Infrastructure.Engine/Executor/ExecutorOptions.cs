using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;
using Orchitect.Infrastructure.Engine.Secret;

namespace Orchitect.Infrastructure.Engine.Executor;

public sealed record ExecutorOptions
{
    public const string SectionName = "ExecutorOptions";

    [Required]
    public required string Image { get; init; }
    public string? Network { get; init; }
    public string? DatabaseHost { get; init; }
    public int? DatabasePort { get; init; }
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public long? MemoryBytes { get; init; } = 2L * 1024 * 1024 * 1024;
    public long? NanoCpus { get; init; } = 2_000_000_000;
    public long? PidsLimit { get; init; } = 512;
    public TimeSpan StopGracePeriod { get; init; } = TimeSpan.FromMinutes(6);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromHours(1);
    public TerraformBackendOptions TerraformBackend { get; init; } = new();
    public SecretProviderOptions SecretProvider { get; init; } = new();

    public Dictionary<string, string> Configuration { get; init; } = [];

    public IReadOnlyDictionary<string, string> ToEnvironment()
    {
        var environment = new Dictionary<string, string>
        {
            ["Logging__LogLevel__Default"] = LogLevel.ToString()
        };

        environment[$"{TerraformBackendOptions.SectionName}__Mode"] = TerraformBackend.Mode.ToString();

        if (TerraformBackend.IsRemote)
        {
            environment[$"{TerraformBackendOptions.SectionName}__Type"] = TerraformBackend.Type!;

            foreach (var (key, value) in TerraformBackend.Config)
            {
                environment[$"{TerraformBackendOptions.SectionName}__Config__{key}"] = value;
            }
        }

        environment[$"{SecretProviderOptions.SectionName}__Type"] = SecretProvider.Type.ToString();

        if (SecretProvider.AzureKeyVault.VaultUri is { } vaultUri)
        {
            environment[$"{SecretProviderOptions.SectionName}__AzureKeyVault__VaultUri"] = vaultUri.ToString();
        }

        foreach (var (key, value) in SecretProvider.Mappings)
        {
            environment[$"{SecretProviderOptions.SectionName}__Mappings__{key}"] = value;
        }

        foreach (var (key, value) in Configuration)
        {
            environment[key] = value;
        }

        return environment;
    }
}
