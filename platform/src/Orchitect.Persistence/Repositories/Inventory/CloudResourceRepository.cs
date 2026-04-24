using Microsoft.EntityFrameworkCore;
using Orchitect.Common.Extensions;
using Orchitect.Common.Query;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Requests;
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

    public IReadOnlyList<CloudResource> GetByQuery(CloudResourceQuery query)
    {
        var cloudResources = GetAll().Where(cr => cr.OrganisationId == query.OrganisationId);

        return new QueryBuilder<CloudResource>(cloudResources)
            .Where(query.Id, p => p.Id.Value == query.Id)
            .Where(query.Name, p => p.Name.Contains(query.Name ?? string.Empty))
            .Where(query.Description, p => p.Description.Contains(query.Description ?? string.Empty))
            .Where(query.Url, p => p.Url.ToString().Contains(query.Url ?? string.Empty))
            .Where(query.Type, p => p.Type.EqualsCaseInsensitive(query.Type))
            .Where(query.Platform, p => p.Platform == query.Platform)
            .ToList();
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