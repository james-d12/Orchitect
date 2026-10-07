using System.Text;
using Orchitect.Storage.FileSystem;

namespace Orchitect.Storage.Unit.Tests;

public sealed class FileSystemStorageProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"orchitect-storage-{Guid.NewGuid():N}");
    private readonly FileSystemStorageProvider _provider;

    public FileSystemStorageProviderTests()
    {
        _provider = new FileSystemStorageProvider(new FileSystemStorageOptions { RootPath = _root });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_ThenOpenReadAsync_ReturnsTheContent()
    {
        var key = StorageKey.Create("runs/abc/log/terraform-init.log");

        await _provider.WriteAsync(key, Content("initialised"));

        Assert.Equal("initialised", await ReadAsync(key));
        Assert.True(File.Exists(Path.Combine(_root, "runs", "abc", "log", "terraform-init.log")));
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
    public async Task WriteAsync_LeavesNoTempFilesBehind()
    {
        await _provider.WriteAsync(StorageKey.Create("runs/abc/plan/plan.json"), Content("{}"));

        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, ".tmp")));
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

    [Fact]
    public async Task ListAsync_MissingPrefix_ReturnsNothing()
    {
        Assert.Empty(await _provider.ListAsync(StorageKey.Create("runs/missing")).ToListAsync());
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
