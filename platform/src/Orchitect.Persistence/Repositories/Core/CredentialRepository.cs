using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Persistence.Repositories.Core;

public sealed class CredentialRepository : ICredentialRepository
{
    private readonly OrchitectDbContext _dbContext;

    public CredentialRepository(OrchitectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Credential?> CreateAsync(Credential credential,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.Credentials.AddAsync(credential, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public IEnumerable<Credential> GetAll()
    {
        return _dbContext.Credentials.AsEnumerable();
    }

    public Task<Credential?> GetByIdAsync(CredentialId id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Credentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken: cancellationToken);
    }

    public IEnumerable<Credential> GetAllByOrganisationId(OrganisationId organisationId)
    {
        return _dbContext.Credentials.AsNoTracking()
            .Where(c => c.OrganisationId == organisationId)
            .AsEnumerable();
    }

    public async Task<Credential?> UpdateAsync(Credential credential,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Credentials.Update(credential);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return credential;
    }

    public async Task<bool> DeleteAsync(CredentialId id, CancellationToken cancellationToken = default)
    {
        var credential = await _dbContext.Credentials.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (credential is null)
        {
            return false;
        }

        _dbContext.Credentials.Remove(credential);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}