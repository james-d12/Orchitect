using Conductor.Inventory.Domain.Ticketing.Request;

namespace Conductor.Inventory.Domain.Ticketing.Service;

public interface ITicketingQueryService
{
    List<WorkItem> QueryWorkItems(WorkItemQueryRequest request);
}