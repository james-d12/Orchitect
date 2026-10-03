namespace Orchitect.Domain.Core;

public interface IUnitOfWork
{
    /// <summary>
    /// Runs the work in one database transaction and commits it only if the work completes. After a transient
    /// failure the work is rolled back and run again from the start, so it must not depend on state from an
    /// earlier attempt.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the work in one database transaction, as <see cref="ExecuteAsync{T}"/> does.
    /// </summary>
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}
