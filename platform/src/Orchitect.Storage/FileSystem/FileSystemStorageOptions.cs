namespace Orchitect.Storage.FileSystem;

public sealed record FileSystemStorageOptions
{
    private const string ConfigPath = $"{StorageOptions.SectionName}:FileSystem";

    public string? RootPath { get; init; }

    public string? GetValidationError() => RootPath switch
    {
        null or "" => $"{ConfigPath}:RootPath is required when {StorageOptions.SectionName}:Type is " +
                      $"{StorageProviderType.FileSystem}.",
        _ when !Path.IsPathFullyQualified(RootPath) => $"{ConfigPath}:RootPath '{RootPath}' must be an absolute path.",
        _ => null
    };
}
