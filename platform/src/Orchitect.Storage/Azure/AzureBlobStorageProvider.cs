using System.Net;
using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Orchitect.Storage.Azure;

public sealed class AzureBlobStorageProvider : IStorageProvider
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageProvider(BlobContainerClient container)
    {
        _container = container;
    }

    public async Task WriteAsync(StorageKey key, Stream content, CancellationToken cancellationToken = default) =>
        await _container.GetBlobClient(key.Value).UploadAsync(content, overwrite: true, cancellationToken);

    public async Task<Stream?> OpenReadAsync(StorageKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _container.GetBlobClient(key.Value)
                .OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default)
    {
        var response = await _container.GetBlobClient(key.Value)
            .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        return response.Value;
    }

    public async IAsyncEnumerable<StorageKey> ListAsync(StorageKey prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var blob in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, $"{prefix.Value}/",
                           cancellationToken: cancellationToken))
        {
            yield return StorageKey.Create(blob.Name);
        }
    }
}
