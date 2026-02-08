using Orchitect.Inventory.Domain.Ticketing.Request;

namespace Orchitect.Inventory.Domain.Ticketing.Service;

public interface ITicketingQueryService
{
    List<WorkItem> QueryWorkItems(WorkItemQueryRequest request);
}