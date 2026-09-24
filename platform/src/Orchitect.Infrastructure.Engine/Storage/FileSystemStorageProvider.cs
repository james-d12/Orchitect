namespace Orchitect.Infrastructure.Engine.Storage;

public sealed class FileSystemStorageProvider : IStorageLogProvider
{
    public Task SaveAsync(StorageLogItem item, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task GetAsync<TOut>(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}