namespace Orchitect.Storage;

public interface IStorageProvider
{
    /// <summary>
    /// Writes the content to the key, replacing anything already stored there.
    /// </summary>
    Task WriteAsync(StorageKey key, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the content stored at the key for reading, or returns null when nothing is stored there.
    /// </summary>
    Task<Stream?> OpenReadAsync(StorageKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the content stored at the key. Returns false when nothing was stored there.
    /// </summary>
    Task<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every key stored under the prefix.
    /// </summary>
    IAsyncEnumerable<StorageKey> ListAsync(StorageKey prefix, CancellationToken cancellationToken = default);
}
