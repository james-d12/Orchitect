using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Discovery.Services;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class DiscoveryRunRepository : IDiscoveryRunRepository
{
    private readonly OrchitectDbContext _dbContext;

    public DiscoveryRunRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DiscoveryRun?> CreateAsync(DiscoveryRun run, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.DiscoveryRuns.AddAsync(run, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }

        return entry.Entity;
    }

    public IEnumerable<DiscoveryRun> GetAll()
    {
        return _dbContext.DiscoveryRuns.AsNoTracking().AsEnumerable();
    }

    public Task<DiscoveryRun?> GetByIdAsync(DiscoveryRunId id, CancellationToken cancellationToken = default)
    {
        return _dbContext.DiscoveryRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<DiscoveryRun?> UpdateAsync(DiscoveryRun run, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.DiscoveryRuns.Update(run);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }

        return entry.Entity;
    }

    public Task<DiscoveryRun?> GetLatestAsync(DiscoveryConfigurationId configurationId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DiscoveryRuns.AsNoTracking()
            .Where(r => r.DiscoveryConfigurationId == configurationId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiscoveryRun>> GetRecentAsync(DiscoveryConfigurationId configurationId, int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DiscoveryRuns.AsNoTracking()
            .Where(r => r.DiscoveryConfigurationId == configurationId)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
