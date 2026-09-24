namespace Orchitect.Infrastructure.Engine.Storage;

public interface IStorageProvider<in T>
{
    public Task SaveAsync(T item, CancellationToken cancellationToken = default);
    public Task GetAsync<TOut>(string name, CancellationToken cancellationToken = default);
}

public record StorageLogItem
{
    public required string Name { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public required DateTime TimeStamp { get; init; }
}

public interface IStorageLogProvider : IStorageProvider<StorageLogItem>
{

}
