using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Storage.Azure;
using Orchitect.Storage.FileSystem;

namespace Orchitect.Storage.Unit.Tests;

public sealed class StorageExtensionsTests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Theory]
    [InlineData(null)]
    [InlineData("None")]
    public void AddStorage_None_RegistersNoProvider(string? type)
    {
        using var provider = new ServiceCollection()
            .AddStorage(Configuration(new() { ["Storage:Type"] = type }))
            .BuildServiceProvider();

        Assert.Null(provider.GetService<IStorageProvider>());
    }

    [Theory]
    [InlineData("FileSystem", typeof(FileSystemStorageProvider))]
    [InlineData("filesystem", typeof(FileSystemStorageProvider))]
    [InlineData("AzureBlob", typeof(AzureBlobStorageProvider))]
    public void AddStorage_RegistersConfiguredProvider(string type, Type expected)
    {
        var configuration = Configuration(new()
        {
            ["Storage:Type"] = type,
            ["Storage:FileSystem:RootPath"] = Path.GetTempPath(),
            ["Storage:AzureBlob:ContainerUri"] = "https://orchitect.blob.core.windows.net/artifacts",
            ["Storage:AzureBlob:AccessToken"] = "blob-token"
        });

        using var provider = new ServiceCollection().AddStorage(configuration).BuildServiceProvider();

        Assert.IsType(expected, provider.GetRequiredService<IStorageProvider>());
    }

    [Theory]
    [InlineData("S3")]
    [InlineData("azure-blob")]
    [InlineData("")]
    public void AddStorage_UnknownType_Throws(string type)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddStorage(Configuration(new() { ["Storage:Type"] = type })));
    }

    [Fact]
    public void AddStorage_InvalidProviderOptions_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddStorage(Configuration(new() { ["Storage:Type"] = "AzureBlob" })));

        Assert.Contains("ContainerUri", exception.Message);
    }
}
