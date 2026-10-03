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

    public async Task<DeploymentRun?> UpdateAsync(DeploymentRun run, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.DeploymentRuns.Update(run);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }

        return run;
    }

    public async Task<bool> TryFinishAsync(DeploymentRun run, CancellationToken cancellationToken = default)
    {
        var updated = await _dbContext.DeploymentRuns
            .Where(r => r.Id == run.Id &&
                        (r.Status == DeploymentRunStatus.Queued || r.Status == DeploymentRunStatus.Running))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, run.Status)
                .SetProperty(r => r.FinishedAt, run.FinishedAt)
                .SetProperty(r => r.ExitCode, run.ExitCode)
                .SetProperty(r => r.ErrorSummary, run.ErrorSummary)
                .SetProperty(r => r.RunnerId, run.RunnerId)
                .SetProperty(r => r.TokenHash, run.TokenHash)
                .SetProperty(r => r.TokenExpiresAt, run.TokenExpiresAt), cancellationToken);

        return updated == 1;
    }
}
