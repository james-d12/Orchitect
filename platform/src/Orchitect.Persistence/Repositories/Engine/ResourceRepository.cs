using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;

namespace Orchitect.Persistence.Repositories.Engine;

public sealed class ResourceRepository : IResourceRepository
{
    private readonly OrchitectDbContext _dbContext;

    public ResourceRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Resource?> CreateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.Resources.AddAsync(resource, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public IEnumerable<Resource> GetAll()
    {
        return _dbContext.Resources.AsEnumerable();
    }

    public Task<Resource?> GetByIdAsync(ResourceId id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Resources.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Resource?> UpdateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        _dbContext.Resources.Update(resource);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return resource;
    }

    public async Task<bool> DeleteAsync(ResourceId id, CancellationToken cancellationToken = default)
    {
        var resource = await _dbContext.Resources.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (resource is null) return false;
        _dbContext.Resources.Remove(resource);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<IReadOnlyList<Resource>> GetByEnvironmentAsync(EnvironmentId environmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Resources.AsNoTracking()
            .Where(r => r.EnvironmentId == environmentId)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<Resource>>(t => t.Result, cancellationToken);
    }
}
