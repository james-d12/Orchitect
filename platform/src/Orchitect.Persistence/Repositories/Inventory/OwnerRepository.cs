using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Git;
using Orchitect.Domain.Inventory.Git.Service;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class OwnerRepository : IOwnerRepository
{
    private readonly OrchitectDbContext _context;

    public OwnerRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Owner> GetAll()
    {
        return _context.Owners
            .OrderBy(o => o.OrganisationId)
            .ThenBy(o => o.Name)
            .ToList();
    }

    public async Task<Owner?> GetByIdAsync(
        OwnerId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Owners
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Owner>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Owners
            .Where(o => o.OrganisationId == organisationId)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Owner?> GetByNameAndPlatformAsync(
        OrganisationId organisationId,
        string name,
        OwnerPlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.Owners
            .FirstOrDefaultAsync(
                o => o.OrganisationId == organisationId &&
                     o.Name == name &&
                     o.Platform == platform,
                cancellationToken);
    }

    public async Task<Owner?> CreateAsync(
        Owner owner,
        CancellationToken cancellationToken = default)
    {
        await _context.Owners.AddAsync(owner, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return owner;
    }

    public async Task UpsertAsync(
        Owner owner,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.Owners
            .FirstOrDefaultAsync(
                o => o.OrganisationId == owner.OrganisationId &&
                     o.Name == owner.Name &&
                     o.Platform == owner.Platform,
                cancellationToken);

        if (existing is null)
        {
            await _context.Owners.AddAsync(owner, cancellationToken);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(owner);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
