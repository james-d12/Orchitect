using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.Pipeline.Services;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class PipelineRepository : IPipelineRepository
{
    private readonly OrchitectDbContext _context;

    public PipelineRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Pipeline> GetAll()
    {
        return _context.Pipelines
            .Include(p => p.User)
            .OrderBy(p => p.OrganisationId)
            .ThenBy(p => p.Name)
            .ToList();
    }

    public async Task<Pipeline?> GetByIdAsync(
        PipelineId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Pipelines
            .Include(p => p.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Pipeline>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Pipelines
            .Include(p => p.User)
            .Where(p => p.OrganisationId == organisationId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Pipeline>> GetByPlatformAsync(
        OrganisationId organisationId,
        PipelinePlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.Pipelines
            .Include(p => p.User)
            .Where(p => p.OrganisationId == organisationId && p.Platform == platform)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Pipeline?> CreateAsync(
        Pipeline pipeline,
        CancellationToken cancellationToken = default)
    {
        await _context.Pipelines.AddAsync(pipeline, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return pipeline;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<Pipeline> pipelines,
        CancellationToken cancellationToken = default)
    {
        foreach (var pipeline in pipelines)
        {
            var existing = await _context.Pipelines
                .FirstOrDefaultAsync(p => p.Url == pipeline.Url, cancellationToken);

            if (existing is null)
            {
                await _context.Pipelines.AddAsync(pipeline, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(pipeline);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
