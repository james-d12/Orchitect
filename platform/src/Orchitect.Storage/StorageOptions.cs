using Orchitect.Storage.Azure;
using Orchitect.Storage.FileSystem;

namespace Orchitect.Storage;

public enum StorageProviderType
{
    None,
    FileSystem,
    AzureBlob
}

public sealed record StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProviderType Type { get; init; } = StorageProviderType.None;

    public FileSystemStorageOptions FileSystem { get; init; } = new();

    public AzureBlobStorageOptions AzureBlob { get; init; } = new();

    public bool IsEnabled => Type != StorageProviderType.None;

    public string? GetValidationError() => Type switch
    {
        StorageProviderType.None => null,
        StorageProviderType.FileSystem => FileSystem.GetValidationError(),
        StorageProviderType.AzureBlob => AzureBlob.GetValidationError(),
        _ => $"{SectionName}:Type '{Type}' is not supported."
    };
}
