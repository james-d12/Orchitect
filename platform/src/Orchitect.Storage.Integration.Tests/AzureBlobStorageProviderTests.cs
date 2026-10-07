using System.Text;
using Azure.Storage.Blobs;
using Orchitect.Storage.Azure;
using Testcontainers.Azurite;

namespace Orchitect.Storage.Integration.Tests;

public sealed class AzureBlobStorageProviderTests : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite =
        new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0").WithInMemoryPersistence()
            .WithCommand("--skipApiVersionCheck").Build();

    private AzureBlobStorageProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _azurite.StartAsync();

        var container = new BlobContainerClient(_azurite.GetConnectionString(), "artifacts");
        await container.CreateAsync();
        _provider = new AzureBlobStorageProvider(container);
    }

    public async Task DisposeAsync() => await _azurite.DisposeAsync();

    [Fact]
    public async Task WriteAsync_ThenOpenReadAsync_ReturnsTheContent()
    {
        var key = StorageKey.Create("runs/abc/log/terraform-init.log");

        await _provider.WriteAsync(key, Content("initialised"));

        Assert.Equal("initialised", await ReadAsync(key));
    }

    [Fact]
    public async Task WriteAsync_ExistingKey_ReplacesTheContent()
    {
        var key = StorageKey.Create("runs/abc/plan/plan.json");

        await _provider.WriteAsync(key, Content("first, and longer"));
        await _provider.WriteAsync(key, Content("second"));

        Assert.Equal("second", await ReadAsync(key));
    }

    [Fact]
    public async Task OpenReadAsync_MissingKey_ReturnsNull()
    {
        Assert.Null(await _provider.OpenReadAsync(StorageKey.Create("runs/missing/log/terraform-init.log")));
    }

    [Fact]
    public async Task DeleteAsync_ReportsWhetherSomethingWasDeleted()
    {
        var key = StorageKey.Create("runs/abc/log/terraform-init.log");
        await _provider.WriteAsync(key, Content("initialised"));

        Assert.True(await _provider.DeleteAsync(key));
        Assert.False(await _provider.DeleteAsync(key));
        Assert.Null(await _provider.OpenReadAsync(key));
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyKeysUnderThePrefix()
    {
        await _provider.WriteAsync(StorageKey.Create("runs/abc/log/terraform-init.log"), Content("a"));
        await _provider.WriteAsync(StorageKey.Create("runs/abc/plan/plan.tfplan"), Content("b"));
        await _provider.WriteAsync(StorageKey.Create("runs/abcd/log/terraform-init.log"), Content("c"));

        var keys = await _provider.ListAsync(StorageKey.Create("runs/abc")).Select(k => k.Value).ToListAsync();

        Assert.Equal(["runs/abc/log/terraform-init.log", "runs/abc/plan/plan.tfplan"], keys.Order());
    }

    private static MemoryStream Content(string value) => new(Encoding.UTF8.GetBytes(value));

    private async Task<string> ReadAsync(StorageKey key)
    {
        await using var stream = await _provider.OpenReadAsync(key);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
