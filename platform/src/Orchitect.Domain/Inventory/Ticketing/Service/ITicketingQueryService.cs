using Orchitect.Domain.Inventory.Ticketing.Request;

namespace Orchitect.Domain.Inventory.Ticketing.Service;

public interface ITicketingQueryService
{
    List<WorkItem> QueryWorkItems(WorkItemQueryRequest request);
}