using Microsoft.EntityFrameworkCore;
using Orchitect.Common.Query;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Cloud.Requests;
using Orchitect.Domain.Inventory.Cloud.Services;

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

    public IReadOnlyList<CloudSecret> GetByQuery(CloudSecretQuery query)
    {
        var cloudSecrets = GetAll();

        return new QueryBuilder<CloudSecret>(cloudSecrets)
            .Where(query.Name, p => p.Name == query.Name)
            .Where(query.Url, p => p.Url.ToString().Contains(query.Url ?? string.Empty))
            .Where(query.Platform, p => p.Platform == query.Platform)
            .ToList();
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