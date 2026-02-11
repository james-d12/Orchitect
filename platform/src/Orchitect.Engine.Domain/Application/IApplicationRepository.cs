using Orchitect.Shared;

namespace Orchitect.Engine.Domain.Application;

public interface IApplicationRepository : IRepository<Application, ApplicationId>
{
    Task<Application?> UpdateAsync(Application application, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ApplicationId id, CancellationToken cancellationToken = default);
}