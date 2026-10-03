using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Persistence.Repositories.Engine;

public sealed class DeploymentRunRepository : IDeploymentRunRepository
{
    private readonly OrchitectDbContext _dbContext;

    public DeploymentRunRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeploymentRun?> CreateAsync(DeploymentRun run, CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.DeploymentRuns.AddAsync(run, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            result.State = EntityState.Detached;
        }

        return result.Entity;
    }

    public IEnumerable<DeploymentRun> GetAll()
    {
        return _dbContext.DeploymentRuns.AsEnumerable();
    }

    public Task<DeploymentRun?> GetByIdAsync(DeploymentRunId id, CancellationToken cancellationToken = default)
    {
        return _dbContext.DeploymentRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<DeploymentRun?> GetLatestAsync(DeploymentId deploymentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DeploymentRuns.AsNoTracking()
            .Where(r => r.DeploymentId == deploymentId)
            .OrderByDescending(r => r.QueuedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<DeploymentRun?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return _dbContext.DeploymentRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);
    }

    public Task LockAsync(DeploymentRunId id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"DeploymentRuns\" WHERE \"Id\" = {id.Value} FOR UPDATE", cancellationToken);
    }

    public async Task<DeploymentRun?> UpdateAsync(DeploymentRun run, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.DeploymentRuns.Update(run);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new DeploymentRunConflictException(run.Id, exception);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }

        return entry.Entity;
    }
}
