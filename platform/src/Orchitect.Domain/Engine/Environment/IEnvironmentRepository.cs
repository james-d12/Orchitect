using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Engine.Environment;

public interface IEnvironmentRepository : IRepository<Environment, EnvironmentId>
{
    Task<Environment?> UpdateAsync(Environment environment, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(EnvironmentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the environments that belong to any of the organisations.
    /// </summary>
    Task<IReadOnlyList<Environment>> GetByOrganisationIdsAsync(IReadOnlyCollection<OrganisationId> organisationIds,
        CancellationToken cancellationToken = default);
}