using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Shared.Service;

public interface ITeamRepository : IRepository<Team, TeamId>
{
    Task<IReadOnlyList<Team>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Team>> GetByPlatformAsync(
        OrganisationId organisationId,
        TeamPlatform platform,
        CancellationToken cancellationToken = default);

    Task<Team?> GetByUrlAsync(
        string url,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<Team> teams,
        CancellationToken cancellationToken = default);
}
