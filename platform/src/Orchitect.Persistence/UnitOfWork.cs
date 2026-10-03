using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core;

namespace Orchitect.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly OrchitectDbContext _dbContext;

    public UnitOfWork(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(token);

            try
            {
                var result = await work(token);
                await transaction.CommitAsync(token);
                return result;
            }
            catch
            {
                _dbContext.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async token =>
        {
            await work(token);
            return true;
        }, cancellationToken);
    }
}
