using Orchitect.Engine.Domain.Deployment;
using Microsoft.EntityFrameworkCore;

namespace Orchitect.Engine.Persistence.Repositories;

public sealed class DeploymentRepository : IDeploymentRepository
{
    private readonly EngineDbContext _dbContext;

    public DeploymentRepository(EngineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Deployment?> CreateAsync(Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.Deployments.AddAsync(deployment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public IEnumerable<Deployment> GetAll()
    {
        return _dbContext.Deployments.AsEnumerable();
    }

    public Task<Deployment?> GetByIdAsync(DeploymentId id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Deployments.FirstOrDefaultAsync(t => t.Id == id, cancellationToken: cancellationToken);
    }
}