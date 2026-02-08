namespace Orchitect.Engine.Domain.Shared;

public interface IRepository<T, in TId>
{
    Task<T?> CreateAsync(T environment,
        CancellationToken cancellationToken = default);

    IEnumerable<T> GetAll();
    Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
}