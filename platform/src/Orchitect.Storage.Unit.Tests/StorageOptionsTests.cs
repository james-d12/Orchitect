using Orchitect.Storage.Azure;
using Orchitect.Storage.FileSystem;

namespace Orchitect.Storage.Unit.Tests;

public sealed class StorageOptionsTests
{
    [Fact]
    public void GetValidationError_Defaults_IsNoneAndValid()
    {
        var options = new StorageOptions();

        Assert.Equal(StorageProviderType.None, options.Type);
        Assert.False(options.IsEnabled);
        Assert.Null(options.GetValidationError());
    }

    [Theory]
    [InlineData(null, "RootPath is required")]
    [InlineData("", "RootPath is required")]
    [InlineData("artifacts", "must be an absolute path")]
    public void GetValidationError_FileSystemWithoutAbsoluteRoot_ReturnsError(string? rootPath, string expected)
    {
        var options = new StorageOptions
        {
            Type = StorageProviderType.FileSystem,
            FileSystem = new FileSystemStorageOptions { RootPath = rootPath }
        };

        Assert.Contains(expected, options.GetValidationError());
    }

    [Theory]
    [InlineData(null, "ContainerUri is required")]
    [InlineData("artifacts", "must be an absolute URI")]
    [InlineData("https://orchitect.blob.core.windows.net/artifacts?sv=2024&sig=secret", "query string")]
    public void GetValidationError_AzureBlobWithoutUsableContainerUri_ReturnsError(string? containerUri,
        string expected)
    {
        var options = new StorageOptions
        {
            Type = StorageProviderType.AzureBlob,
            AzureBlob = new AzureBlobStorageOptions
            {
                ContainerUri = containerUri is null ? null : new Uri(containerUri, UriKind.RelativeOrAbsolute)
            }
        };

        Assert.Contains(expected, options.GetValidationError());
    }

    [Fact]
    public void GetValidationError_UnknownType_ReturnsError()
    {
        Assert.Contains("not supported", new StorageOptions { Type = (StorageProviderType)42 }.GetValidationError());
    }
}
