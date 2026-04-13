using Microsoft.EntityFrameworkCore;
using Orchitect.Common.Query;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.Pipeline.Requests;
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

    public IReadOnlyList<Pipeline> GetByQuery(PipelineQuery query)
    {
        var pipelines = GetAll();

        return new QueryBuilder<Pipeline>(pipelines)
            .Where(query.Id, p => p.Id.Value == query.Id)
            .Where(query.Name, p => p.Name.Contains(query.Name ?? string.Empty))
            .Where(query.Url, p => p.Url.ToString().Contains(query.Url ?? string.Empty))
            .Where(query.OwnerName, p => p.User.Name.Contains(query.OwnerName ?? string.Empty))
            .Where(query.Platform, p => p.Platform == query.Platform)
            .ToList();
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
