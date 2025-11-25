using Conductor.Engine.Domain.Organisation;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Engine.Persistence.Repositories;

public sealed class OrganisationRepository : IOrganisationRepository
{
    private readonly ConductorDbContext _dbContext;

    public OrganisationRepository(ConductorDbContext dbContext)
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
        return _dbContext.Organisations.FirstOrDefaultAsync(t => t.Id == id, cancellationToken: cancellationToken);
    }
}