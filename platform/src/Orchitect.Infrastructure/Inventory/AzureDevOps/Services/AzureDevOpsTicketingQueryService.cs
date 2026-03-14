using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Domain.Inventory.Ticketing;
using Orchitect.Domain.Inventory.Ticketing.Request;
using Orchitect.Domain.Inventory.Ticketing.Service;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Constants;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Models;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using Orchitect.Infrastructure.Inventory.Shared.Query;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public sealed class AzureDevOpsTicketingQueryService : ITicketingQueryService
{
    private readonly ILogger<AzureDevOpsTicketingQueryService> _logger;
    private readonly IMemoryCache _memoryCache;

    public AzureDevOpsTicketingQueryService(
        ILogger<AzureDevOpsTicketingQueryService> logger,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
    }

    public List<WorkItem> QueryWorkItems(WorkItemQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying work items from Azure DevOps");
        var azureWorkItems = _memoryCache.Get<List<AzureDevOpsWorkItem>>(AzureDevOpsCacheConstants.WorkItemsCacheKey) ??
                             [];
        var workItems = azureWorkItems.ConvertAll<WorkItem>(p => p);

        return new QueryBuilder<WorkItem>(workItems)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .ToList();
    }
}