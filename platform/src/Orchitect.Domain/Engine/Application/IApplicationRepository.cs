using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Engine.Application;

public interface IApplicationRepository : IRepository<Application, ApplicationId>
{
    Task<Application?> UpdateAsync(Application application, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the applications that belong to an organisation.
    /// </summary>
    Task<IReadOnlyList<Application>> GetByOrganisationAsync(OrganisationId organisationId,
        CancellationToken cancellationToken = default);
}