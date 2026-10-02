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

    public Task<bool> IsMemberAsync(OrganisationId id, string identityUserId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<OrganisationUser>()
            .AnyAsync(u => u.OrganisationId == id && u.IdentityUserId == identityUserId, cancellationToken);
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