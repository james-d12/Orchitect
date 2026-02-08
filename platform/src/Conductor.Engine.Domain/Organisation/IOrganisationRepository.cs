using Conductor.Engine.Domain.Shared;

namespace Conductor.Engine.Domain.Organisation;

public interface IOrganisationRepository : IRepository<Organisation, OrganisationId>
{
    Task<Organisation?> UpdateAsync(Organisation organisation, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(OrganisationId id, CancellationToken cancellationToken = default);
}