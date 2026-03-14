using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Ticketing;
using Orchitect.Domain.Inventory.Ticketing.Service;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class WorkItemRepository : IWorkItemRepository
{
    private readonly OrchitectDbContext _context;

    public WorkItemRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<WorkItem> GetAll()
    {
        return _context.WorkItems
            .OrderBy(wi => wi.OrganisationId)
            .ThenBy(wi => wi.Title)
            .ToList();
    }

    public async Task<WorkItem?> GetByIdAsync(
        WorkItemId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(wi => wi.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .Where(wi => wi.OrganisationId == organisationId)
            .OrderBy(wi => wi.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> GetByPlatformAsync(
        OrganisationId organisationId,
        WorkItemPlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .Where(wi => wi.OrganisationId == organisationId && wi.Platform == platform)
            .OrderBy(wi => wi.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkItem?> CreateAsync(
        WorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        await _context.WorkItems.AddAsync(workItem, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return workItem;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<WorkItem> workItems,
        CancellationToken cancellationToken = default)
    {
        foreach (var workItem in workItems)
        {
            var existing = await _context.WorkItems
                .FirstOrDefaultAsync(wi => wi.Url == workItem.Url, cancellationToken);

            if (existing is null)
            {
                await _context.WorkItems.AddAsync(workItem, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(workItem);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
