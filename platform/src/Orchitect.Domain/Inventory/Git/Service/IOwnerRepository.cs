using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Git.Service;

public interface IOwnerRepository : IRepository<Owner, OwnerId>
{
    Task<IReadOnlyList<Owner>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<Owner?> GetByNameAndPlatformAsync(
        OrganisationId organisationId,
        string name,
        OwnerPlatform platform,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        Owner owner,
        CancellationToken cancellationToken = default);
}
