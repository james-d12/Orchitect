using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Git;
using Orchitect.Domain.Inventory.Git.Service;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class RepositoryRepository : IRepositoryRepository
{
    private readonly OrchitectDbContext _context;

    public RepositoryRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Repository> GetAll()
    {
        return _context.Repositories
            .Include(r => r.Owner)
            .OrderBy(r => r.OrganisationId)
            .ThenBy(r => r.Name)
            .ToList();
    }

    public async Task<Repository?> GetByIdAsync(
        RepositoryId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Repositories
            .Include(r => r.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Repository>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Repositories
            .Include(r => r.Owner)
            .Where(r => r.OrganisationId == organisationId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Repository>> GetByPlatformAsync(
        OrganisationId organisationId,
        RepositoryPlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.Repositories
            .Include(r => r.Owner)
            .Where(r => r.OrganisationId == organisationId && r.Platform == platform)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Repository?> GetByUrlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        return await _context.Repositories
            .Include(r => r.Owner)
            .FirstOrDefaultAsync(r => r.Url.ToString() == url, cancellationToken);
    }

    public async Task<Repository?> CreateAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        await _context.Repositories.AddAsync(repository, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return repository;
    }

    public async Task UpsertAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.Repositories
            .FirstOrDefaultAsync(r => r.Url == repository.Url, cancellationToken);

        if (existing is null)
        {
            await _context.Repositories.AddAsync(repository, cancellationToken);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(repository);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(
        IEnumerable<Repository> repositories,
        CancellationToken cancellationToken = default)
    {
        foreach (var repository in repositories)
        {
            var existing = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Url == repository.Url, cancellationToken);

            if (existing is null)
            {
                await _context.Repositories.AddAsync(repository, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(repository);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
