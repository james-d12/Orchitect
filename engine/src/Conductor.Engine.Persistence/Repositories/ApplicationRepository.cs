using Conductor.Engine.Domain.Application;
using Microsoft.EntityFrameworkCore;
using ApplicationId = Conductor.Engine.Domain.Application.ApplicationId;

namespace Conductor.Engine.Persistence.Repositories;

public sealed class ApplicationRepository : IApplicationRepository
{
    private readonly ConductorDbContext _dbContext;

    public ApplicationRepository(ConductorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Application?> CreateAsync(Application application,
        CancellationToken cancellationToken = default)
    {
        var result = await _dbContext.Applications.AddAsync(application, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result.Entity;
    }

    public IEnumerable<Application> GetAll()
    {
        return _dbContext.Applications.AsEnumerable();
    }

    public Task<Application?> GetByIdAsync(ApplicationId id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Applications.FirstOrDefaultAsync(t => t.Id == id, cancellationToken: cancellationToken);
    }

    public async Task<Application?> UpdateAsync(Application application,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Applications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return application;
    }

    public async Task<bool> DeleteAsync(ApplicationId id, CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.Applications.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (application is null)
        {
            return false;
        }

        _dbContext.Applications.Remove(application);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}