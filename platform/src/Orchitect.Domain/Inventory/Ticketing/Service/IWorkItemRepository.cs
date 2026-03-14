using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Ticketing.Service;

public interface IWorkItemRepository : IRepository<WorkItem, WorkItemId>
{
    Task<IReadOnlyList<WorkItem>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetByPlatformAsync(
        OrganisationId organisationId,
        WorkItemPlatform platform,
        CancellationToken cancellationToken = default);

    Task BulkUpsertAsync(
        IEnumerable<WorkItem> workItems,
        CancellationToken cancellationToken = default);
}
