using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Orchitect.Engine.Contracts.Secret;
using Orchitect.Engine.Contracts.Terraform;
using Orchitect.Storage;

namespace Orchitect.Engine.Dispatch.Executor;

public sealed record ExecutorOptions
{
    public const string SectionName = "ExecutorOptions";

    [Required]
    public required string Image { get; init; }
    public string? Network { get; init; }
    public Uri? ApiBaseUrl { get; init; }
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public long? MemoryBytes { get; init; } = 2L * 1024 * 1024 * 1024;
    public long? NanoCpus { get; init; } = 2_000_000_000;
    public long? PidsLimit { get; init; } = 512;
    public TimeSpan StopGracePeriod { get; init; } = TimeSpan.FromMinutes(6);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromHours(1);
    public TerraformBackendOptions TerraformBackend { get; init; } = new();
    public SecretProviderOptions SecretProvider { get; init; } = new();
    public StorageOptions Storage { get; init; } = new();
    public string? StorageHostPath { get; init; }

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

        environment[$"{StorageOptions.SectionName}__Type"] = Storage.Type.ToString();

        switch (Storage.Type)
        {
            case StorageProviderType.FileSystem:
                environment[$"{StorageOptions.SectionName}__FileSystem__RootPath"] = Storage.FileSystem.RootPath!;
                break;
            case StorageProviderType.AzureBlob:
                environment[$"{StorageOptions.SectionName}__AzureBlob__ContainerUri"] =
                    Storage.AzureBlob.ContainerUri!.ToString();
                break;
        }

        return environment;
    }

    public IReadOnlyList<string> ToBinds() => StorageHostPath is { Length: > 0 } hostPath
        ? [$"{hostPath}:{Storage.FileSystem.RootPath}"]
        : [];

    public string? GetStorageHostPathValidationError() => StorageHostPath switch
    {
        null => null,
        _ when Storage.Type != StorageProviderType.FileSystem =>
            $"{SectionName}:StorageHostPath is only used when {SectionName}:Storage:Type is " +
            $"{StorageProviderType.FileSystem}.",
        _ when !Path.IsPathFullyQualified(StorageHostPath) =>
            $"{SectionName}:StorageHostPath '{StorageHostPath}' must be an absolute path.",
        _ => null
    };
}
