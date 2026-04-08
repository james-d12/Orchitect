using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Identity;
using Orchitect.Domain.Inventory.Identity.Services;

namespace Orchitect.Persistence.Repositories.Inventory;

public sealed class UserRepository : IUserRepository
{
    private readonly OrchitectDbContext _context;

    public UserRepository(OrchitectDbContext context)
    {
        _context = context;
    }

    public IEnumerable<User> GetAll()
    {
        return _context.Owners
            .OrderBy(o => o.OrganisationId)
            .ThenBy(o => o.Name)
            .ToList();
    }

    public async Task<User?> GetByIdAsync(
        UserId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Owners
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Owners
            .Where(o => o.OrganisationId == organisationId)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByNameAndPlatformAsync(
        OrganisationId organisationId,
        string name,
        UserPlatform platform,
        CancellationToken cancellationToken = default)
    {
        return await _context.Owners
            .FirstOrDefaultAsync(
                o => o.OrganisationId == organisationId &&
                     o.Name == name &&
                     o.Platform == platform,
                cancellationToken);
    }

    public async Task<User?> CreateAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _context.Owners.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpsertAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.Owners
            .FirstOrDefaultAsync(
                o => o.OrganisationId == user.OrganisationId &&
                     o.Name == user.Name &&
                     o.Platform == user.Platform,
                cancellationToken);

        if (existing is null)
        {
            await _context.Owners.AddAsync(user, cancellationToken);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(user);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
