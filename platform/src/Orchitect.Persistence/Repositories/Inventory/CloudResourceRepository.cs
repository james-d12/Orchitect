using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Services;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class CloudResourceRepository : ICloudResourceRepository
{
    private readonly OrchitectDbContext _context;

    public CloudResourceRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<CloudResource> GetAll()
    {
        return _context.CloudResources
            .OrderBy(cr => cr.OrganisationId)
            .ThenBy(cr => cr.Name)
            .ToList();
    }

    public async Task<CloudResource?> GetByIdAsync(
        CloudResourceId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.CloudResources
            .AsNoTracking()
            .FirstOrDefaultAsync(cr => cr.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CloudResource>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CloudResources
            .Where(cr => cr.OrganisationId == organisationId)
            .OrderBy(cr => cr.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CloudResource>> GetByPlatformAsync(
        OrganisationId organisationId,
        CloudPlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.CloudResources
            .Where(cr => cr.OrganisationId == organisationId && cr.Platform == platform)
            .OrderBy(cr => cr.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<CloudResource?> CreateAsync(
        CloudResource cloudResource,
        CancellationToken cancellationToken = default)
    {
        await _context.CloudResources.AddAsync(cloudResource, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return cloudResource;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<CloudResource> cloudResources,
        CancellationToken cancellationToken = default)
    {
        foreach (var cloudResource in cloudResources)
        {
            var existing = await _context.CloudResources
                .FirstOrDefaultAsync(cr => cr.Url == cloudResource.Url, cancellationToken);

            if (existing is null)
            {
                await _context.CloudResources.AddAsync(cloudResource, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(cloudResource);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
