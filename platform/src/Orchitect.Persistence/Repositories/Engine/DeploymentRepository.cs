using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Persistence.Configurations.Engine;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Persistence.Repositories.Engine;

public sealed class DeploymentRepository : IDeploymentRepository
{
    private readonly OrchitectDbContext _dbContext;

    public DeploymentRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Deployment?> CreateAsync(Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.Deployments.AddAsync(deployment, cancellationToken);

        try
        {
            await SaveChangesAsync(deployment, cancellationToken);
        }
        catch
        {
            result.State = EntityState.Detached;
            throw;
        }

        return result.Entity;
    }

    public IEnumerable<Deployment> GetAll()
    {
        return _dbContext.Deployments.AsEnumerable();
    }

    public Task<Deployment?> GetByIdAsync(DeploymentId id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Deployments.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken: cancellationToken);
    }

    public Task<Deployment?> GetLatestAsync(ApplicationId applicationId, EnvironmentId environmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Deployments.AsNoTracking()
            .Where(d => d.ApplicationId == applicationId && d.EnvironmentId == environmentId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Deployment>> GetActiveAsync(DateTime updatedBefore,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Deployments.AsNoTracking()
            .Where(d => (d.Status == DeploymentStatus.Pending || d.Status == DeploymentStatus.Deploying ||
                         d.Status == DeploymentStatus.Destroying) && d.UpdatedAt < updatedBefore)
            .ToListAsync(cancellationToken);
    }

    public async Task<Deployment?> UpdateAsync(Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Deployments.Update(deployment);

        try
        {
            await SaveChangesAsync(deployment, cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }

        return deployment;
    }

    private async Task SaveChangesAsync(Deployment deployment, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                                                  {
                                                      SqlState: PostgresErrorCodes.UniqueViolation,
                                                      ConstraintName: DeploymentIndexes.ActiveRun
                                                  })
        {
            throw new ActiveDeploymentExistsException(deployment.ApplicationId, deployment.EnvironmentId, exception);
        }
    }
}