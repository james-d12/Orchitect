using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceInstance;

namespace Orchitect.Persistence.Repositories.Engine;

public sealed class ResourceInstanceRepository : IResourceInstanceRepository
{
    private readonly OrchitectDbContext _dbContext;

    public ResourceInstanceRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResourceInstance?> CreateAsync(ResourceInstance instance,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.ResourceInstances.AddAsync(instance, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public IEnumerable<ResourceInstance> GetAll()
    {
        return _dbContext.ResourceInstances.AsEnumerable();
    }

    public Task<ResourceInstance?> GetByIdAsync(ResourceInstanceId id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ResourceInstances.AsNoTracking()
            .FirstOrDefaultAsync(ri => ri.Id == id, cancellationToken);
    }

    public async Task<ResourceInstance?> UpdateAsync(ResourceInstance instance,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ResourceInstances.Update(instance);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return instance;
    }

    public async Task<bool> DeleteAsync(ResourceInstanceId id, CancellationToken cancellationToken = default)
    {
        var instance = await _dbContext.ResourceInstances
            .FirstOrDefaultAsync(ri => ri.Id == id, cancellationToken);
        if (instance is null) return false;
        _dbContext.ResourceInstances.Remove(instance);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<IReadOnlyList<ResourceInstance>> GetByResourceAsync(ResourceId resourceId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ResourceInstances.AsNoTracking()
            .Where(ri => ri.ResourceId == resourceId)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<ResourceInstance>>(t => t.Result, cancellationToken);
    }

    public Task<IReadOnlyList<ResourceInstance>> GetByEnvironmentAsync(EnvironmentId environmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ResourceInstances.AsNoTracking()
            .Where(ri => ri.EnvironmentId == environmentId)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<ResourceInstance>>(t => t.Result, cancellationToken);
    }
}
