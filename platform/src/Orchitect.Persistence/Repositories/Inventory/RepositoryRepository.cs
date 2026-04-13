using Microsoft.EntityFrameworkCore;
using Orchitect.Common.Query;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Requests;
using Orchitect.Domain.Inventory.SourceControl.Services;

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
            .Include(r => r.User)
            .OrderBy(r => r.OrganisationId)
            .ThenBy(r => r.Name)
            .ToList();
    }

    public async Task<Repository?> GetByIdAsync(
        RepositoryId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Repositories
            .Include(r => r.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public IReadOnlyList<Repository> GetByQuery(RepositoryQuery query)
    {
        var repositories = GetAll();

        return new QueryBuilder<Repository>(repositories)
            .Where(query.Id, p => p.Id.Value == query.Id)
            .Where(query.Name, p => p.Name.Contains(query.Name ?? string.Empty))
            .Where(query.Url, p => p.Url.ToString().Contains(query.Url ?? string.Empty))
            .Where(query.DefaultBranch, p => p.DefaultBranch.Contains(query.DefaultBranch ?? string.Empty))
            .Where(query.OwnerName, p => p.User.Name.Contains(query.OwnerName ?? string.Empty))
            .Where(query.Platform, p => p.Platform == query.Platform)
            .ToList();
    }

    public async Task<Repository?> CreateAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        await _context.Repositories.AddAsync(repository, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return repository;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<Repository> repositories,
        CancellationToken cancellationToken = default)
    {
        foreach (var repository in repositories)
        {
            // Handle owner first to avoid tracking conflicts
            var owner = repository.User;

            // Check if owner is already tracked in the local context
            var trackedOwner = _context.Owners.Local.FirstOrDefault(
                o => o.OrganisationId == owner.OrganisationId &&
                     o.Name == owner.Name &&
                     o.Platform == owner.Platform);

            if (trackedOwner == null)
            {
                // Not tracked locally, check database
                trackedOwner = await _context.Owners.FirstOrDefaultAsync(
                    o => o.OrganisationId == owner.OrganisationId &&
                         o.Name == owner.Name &&
                         o.Platform == owner.Platform,
                    cancellationToken);

                if (trackedOwner == null)
                {
                    // New owner, add to context
                    _context.Owners.Add(owner);
                    trackedOwner = owner;
                }
                else
                {
                    // Existing owner in DB, update and track it
                    _context.Entry(trackedOwner).CurrentValues.SetValues(owner);
                }
            }
            else if (trackedOwner != owner)
            {
                // Already tracked, just update its values
                _context.Entry(trackedOwner).CurrentValues.SetValues(owner);
            }

            // Now handle repository with the properly tracked owner
            var existing = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Url == repository.Url, cancellationToken);

            if (existing is null)
            {
                // Create new repository with tracked owner reference
                var newRepo = repository with { User = trackedOwner };
                await _context.Repositories.AddAsync(newRepo, cancellationToken);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(repository);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
