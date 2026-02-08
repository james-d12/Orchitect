using Orchitect.Engine.Domain.Shared;

namespace Orchitect.Engine.Domain.Organisation;

public interface IOrganisationRepository : IRepository<Organisation, OrganisationId>
{
    Task<Organisation?> UpdateAsync(Organisation organisation, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(OrganisationId id, CancellationToken cancellationToken = default);
}