using Conductor.Inventory.Domain.Ticketing;
using Conductor.Inventory.Domain.Ticketing.Request;
using Conductor.Inventory.Domain.Ticketing.Service;
using Conductor.Inventory.Infrastructure.Shared.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Conductor.Inventory.Api.Controllers;

[ApiController]
[Route("ticketing/")]
public sealed class TicketingController : ControllerBase
{
    private readonly IEnumerable<ITicketingQueryService> _queryServices;

    public TicketingController(IEnumerable<ITicketingQueryService> queryServices)
    {
        _queryServices = queryServices;
    }

    [HttpGet, Route("work-items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public List<WorkItem> GetPipelines([FromQuery] WorkItemQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        var workItems = new List<WorkItem>();
        foreach (var queryService in _queryServices)
        {
            workItems.AddRange(queryService.QueryWorkItems(request));
        }

        return workItems;
    }
}