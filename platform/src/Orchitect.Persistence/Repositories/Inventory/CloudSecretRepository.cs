using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Service;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class CloudSecretRepository : ICloudSecretRepository
{
    private readonly OrchitectDbContext _context;

    public CloudSecretRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<CloudSecret> GetAll()
    {
        return _context.CloudSecrets
            .OrderBy(cs => cs.OrganisationId)
            .ThenBy(cs => cs.Name)
            .ToList();
    }

    public async Task<CloudSecret?> GetByIdAsync(
        CloudSecretId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.CloudSecrets
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CloudSecret>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CloudSecrets
            .Where(cs => cs.OrganisationId == organisationId)
            .OrderBy(cs => cs.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CloudSecret>> GetByPlatformAsync(
        OrganisationId organisationId,
        CloudSecretPlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.CloudSecrets
            .Where(cs => cs.OrganisationId == organisationId && cs.Platform == platform)
            .OrderBy(cs => cs.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<CloudSecret?> GetByUrlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        return await _context.CloudSecrets
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.Url.ToString() == url, cancellationToken);
    }

    public async Task<CloudSecret?> CreateAsync(
        CloudSecret cloudSecret,
        CancellationToken cancellationToken = default)
    {
        await _context.CloudSecrets.AddAsync(cloudSecret, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return cloudSecret;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<CloudSecret> cloudSecrets,
        CancellationToken cancellationToken = default)
    {
        foreach (var cloudSecret in cloudSecrets)
        {
            var existing = await _context.CloudSecrets
                .FirstOrDefaultAsync(cs => cs.Url == cloudSecret.Url, cancellationToken);

            if (existing is null)
            {
                await _context.CloudSecrets.AddAsync(cloudSecret, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(cloudSecret);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
