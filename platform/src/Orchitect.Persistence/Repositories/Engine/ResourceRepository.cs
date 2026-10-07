using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
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

    public async Task<IReadOnlyList<Resource>> GetByOrganisationIdsAsync(
        IReadOnlyCollection<OrganisationId> organisationIds, EnvironmentId? environmentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Resources.AsNoTracking()
            .Where(r => organisationIds.Contains(r.OrganisationId));

        if (environmentId is not null)
        {
            query = query.Where(r => r.EnvironmentId == environmentId.Value);
        }

        return await query.OrderBy(r => r.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Resource>> GetByIdsAsync(IReadOnlyCollection<ResourceId> ids,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Resources.AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }
}
