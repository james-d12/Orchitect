using Orchitect.Inventory.Domain.Ticketing;
using Orchitect.Inventory.Domain.Ticketing.Request;
using Orchitect.Inventory.Domain.Ticketing.Service;
using Orchitect.Inventory.Infrastructure.Shared.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Constants;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Models;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Inventory.Infrastructure.Shared.Query;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Services;

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