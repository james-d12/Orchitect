using System.Runtime.CompilerServices;

namespace Orchitect.Storage.FileSystem;

public sealed class FileSystemStorageProvider : IStorageProvider
{
    private const string TempDirectoryName = ".tmp";
    private const int BufferSize = 81920;

    private readonly string _rootPath;

    public FileSystemStorageProvider(FileSystemStorageOptions options)
    {
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            options.RootPath ?? throw new ArgumentException("RootPath is required.", nameof(options))));
    }

    public async Task WriteAsync(StorageKey key, Stream content, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        var tempDirectory = Path.Combine(_rootPath, TempDirectoryName);
        var tempPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.tmp");

        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             BufferSize, useAsync: true))
            {
                await content.CopyToAsync(file, cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    public Task<Stream?> OpenReadAsync(StorageKey key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);

        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public Task<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);

        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }

    public async IAsyncEnumerable<StorageKey> ListAsync(StorageKey prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var directory = Resolve(prefix);

        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return StorageKey.Create(Path.GetRelativePath(_rootPath, file)
                .Replace(Path.DirectorySeparatorChar, '/'));
        }

        await Task.CompletedTask;
    }

    private string Resolve(StorageKey key)
    {
        var path = Path.GetFullPath(Path.Combine(_rootPath, key.Value));

        if (!path.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Storage key '{key}' resolves outside the storage root.", nameof(key));
        }

        return path;
    }
}
