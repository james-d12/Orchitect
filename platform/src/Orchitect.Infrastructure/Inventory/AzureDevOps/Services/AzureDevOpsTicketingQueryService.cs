using Microsoft.Extensions.Logging;
using Orchitect.Domain.Inventory.Ticketing;
using Orchitect.Domain.Inventory.Ticketing.Request;
using Orchitect.Domain.Inventory.Ticketing.Service;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using Orchitect.Infrastructure.Inventory.Shared.Query;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public sealed class AzureDevOpsTicketingQueryService : ITicketingQueryService
{
    private readonly ILogger<AzureDevOpsTicketingQueryService> _logger;
    private readonly IWorkItemRepository _workItemRepository;

    public AzureDevOpsTicketingQueryService(
        ILogger<AzureDevOpsTicketingQueryService> logger,
        IWorkItemRepository workItemRepository)
    {
        _logger = logger;
        _workItemRepository = workItemRepository;
    }

    public List<WorkItem> QueryWorkItems(WorkItemQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying work items from database for organisation {OrganisationId}", request.OrganisationId);

        var workItems = _workItemRepository
            .GetByPlatformAsync(request.OrganisationId, WorkItemPlatform.AzureDevOps)
            .GetAwaiter()
            .GetResult()
            .ToList();

        return new QueryBuilder<WorkItem>(workItems)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .ToList();
    }
}