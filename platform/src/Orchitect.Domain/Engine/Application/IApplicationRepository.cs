using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Engine.Application;

public interface IApplicationRepository : IRepository<Application, ApplicationId>
{
    Task<Application?> UpdateAsync(Application application, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the applications that belong to any of the organisations.
    /// </summary>
    Task<IReadOnlyList<Application>> GetByOrganisationIdsAsync(IReadOnlyCollection<OrganisationId> organisationIds,
        CancellationToken cancellationToken = default);
}