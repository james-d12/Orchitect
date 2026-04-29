using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.ResourceDependency;

namespace Orchitect.Persistence.Repositories.Engine;

public sealed class ResourceDependencyGraphRepository : IResourceDependencyGraphRepository
{
    private readonly OrchitectDbContext _dbContext;

    public ResourceDependencyGraphRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResourceDependencyGraph?> CreateAsync(ResourceDependencyGraph graph,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.ResourceDependencyGraphs.AddAsync(graph, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public IEnumerable<ResourceDependencyGraph> GetAll()
    {
        return _dbContext.ResourceDependencyGraphs.AsEnumerable();
    }

    public Task<ResourceDependencyGraph?> GetByIdAsync(ResourceDependencyGraphId id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ResourceDependencyGraphs.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<ResourceDependencyGraph?> UpdateAsync(ResourceDependencyGraph graph,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ResourceDependencyGraphs.Update(graph);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return graph;
    }

    public Task<ResourceDependencyGraph?> GetByEnvironmentAsync(EnvironmentId environmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ResourceDependencyGraphs.AsNoTracking()
            .FirstOrDefaultAsync(g => g.EnvironmentId == environmentId, cancellationToken);
    }
}
