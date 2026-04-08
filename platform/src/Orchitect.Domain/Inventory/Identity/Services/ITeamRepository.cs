using Orchitect.Domain.Core;

namespace Orchitect.Domain.Inventory.Identity.Services;

public interface ITeamRepository : IRepository<Team, TeamId>
{
    Task BulkUpsertAsync(
        IEnumerable<Team> teams,
        CancellationToken cancellationToken = default);
}