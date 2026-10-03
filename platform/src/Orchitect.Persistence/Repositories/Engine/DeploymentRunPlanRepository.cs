using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Persistence.Repositories.Engine;

public sealed class DeploymentRunPlanRepository : IDeploymentRunPlanRepository
{
    private readonly OrchitectDbContext _dbContext;

    public DeploymentRunPlanRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<DeploymentRunPlan?> GetByRunIdAsync(DeploymentRunId runId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DeploymentRunPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.RunId == runId, cancellationToken);
    }

    public async Task<DeploymentRunPlan?> CreateAsync(DeploymentRunPlan plan,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.DeploymentRunPlans.AddAsync(plan, cancellationToken);

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
}
