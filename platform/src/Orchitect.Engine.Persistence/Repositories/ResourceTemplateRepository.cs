using Orchitect.Engine.Domain.ResourceTemplate;
using Microsoft.EntityFrameworkCore;

namespace Orchitect.Engine.Persistence.Repositories;

public sealed class ResourceTemplateRepository : IResourceTemplateRepository
{
    private readonly EngineDbContext _dbContext;

    public ResourceTemplateRepository(EngineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResourceTemplate?> CreateAsync(ResourceTemplate resourceTemplate,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.ResourceTemplates.AddAsync(resourceTemplate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public IEnumerable<ResourceTemplate> GetAll()
    {
        return _dbContext.ResourceTemplates.AsEnumerable();
    }

    public Task<ResourceTemplate?> GetByIdAsync(ResourceTemplateId id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ResourceTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken: cancellationToken);
    }

    public Task<ResourceTemplate?> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        return _dbContext.ResourceTemplates.FirstOrDefaultAsync(t => t.Type == type, cancellationToken);
    }

    public async Task<ResourceTemplate?> UpdateAsync(ResourceTemplate resourceTemplate,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ResourceTemplates.Update(resourceTemplate);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return resourceTemplate;
    }

    public async Task<bool> DeleteAsync(ResourceTemplateId id, CancellationToken cancellationToken = default)
    {
        var resourceTemplate = await _dbContext.ResourceTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (resourceTemplate is null)
        {
            return false;
        }

        _dbContext.ResourceTemplates.Remove(resourceTemplate);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}