using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Persistence.Repositories.Core;

public sealed class OrganisationRepository : IOrganisationRepository
{
    private readonly OrchitectDbContext _dbContext;

    public OrganisationRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Organisation?> CreateAsync(Organisation organisation,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.Organisations.AddAsync(organisation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public IEnumerable<Organisation> GetAll()
    {
        return _dbContext.Organisations.AsEnumerable();
    }

    public Task<Organisation?> GetByIdAsync(OrganisationId id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Organisations.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken: cancellationToken);
    }

    public async Task<Organisation?> UpdateAsync(Organisation organisation,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Organisations.Update(organisation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return organisation;
    }

    public async Task<bool> DeleteAsync(OrganisationId id, CancellationToken cancellationToken = default)
    {
        var organisation = await _dbContext.Organisations.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (organisation is null)
        {
            return false;
        }

        _dbContext.Organisations.Remove(organisation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Organisation>> GetByIdsAsync(IReadOnlyCollection<OrganisationId> ids,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Organisations.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<Organisation?> GetWithUsersAndTeamsAsync(OrganisationId id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Organisations.AsNoTracking()
            .Include(o => o.Users)
            .Include(o => o.Teams)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public Task LockAsync(OrganisationId id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Organisations\" WHERE \"Id\" = {id.Value} FOR UPDATE", cancellationToken);
    }

    public async Task UpdateUsersAsync(Organisation organisation, CancellationToken cancellationToken = default)
    {
        var users = _dbContext.Set<OrganisationUser>();
        var existingUsers = await users
            .Where(u => u.OrganisationId == organisation.Id)
            .ToListAsync(cancellationToken);
        var userIds = organisation.Users.Select(u => u.Id).ToHashSet();
        var existingUserIds = existingUsers.Select(u => u.Id).ToHashSet();

        users.RemoveRange(existingUsers.Where(u => !userIds.Contains(u.Id)));
        await users.AddRangeAsync(organisation.Users.Where(u => !existingUserIds.Contains(u.Id)), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.Organisations
            .Where(o => o.Id == organisation.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.UpdatedAt, organisation.UpdatedAt), cancellationToken);
    }

    public async Task<IReadOnlyList<OrganisationId>> GetIdsForMemberAsync(string identityUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<OrganisationUser>()
            .Where(u => u.IdentityUserId == identityUserId)
            .Select(u => u.OrganisationId)
            .ToListAsync(cancellationToken);
    }
}