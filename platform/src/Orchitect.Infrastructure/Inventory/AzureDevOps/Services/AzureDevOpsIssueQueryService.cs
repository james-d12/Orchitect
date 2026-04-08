using Microsoft.Extensions.Logging;
using Orchitect.Domain.Inventory.Issue;
using Orchitect.Domain.Inventory.Issue.Requests;
using Orchitect.Domain.Inventory.Issue.Services;
using Orchitect.Infrastructure.Inventory.Shared.Extensions;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using Orchitect.Infrastructure.Inventory.Shared.Query;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

public sealed class AzureDevOpsIssueQueryService : IIssueQueryService
{
    private readonly ILogger<AzureDevOpsIssueQueryService> _logger;
    private readonly IIssueRepository _issueRepository;

    public AzureDevOpsIssueQueryService(
        ILogger<AzureDevOpsIssueQueryService> logger,
        IIssueRepository issueRepository)
    {
        _logger = logger;
        _issueRepository = issueRepository;
    }

    public List<Issue> QueryWorkItems(IssueQueryRequest request)
    {
        using var activity = Tracing.StartActivity();
        _logger.LogInformation("Querying work items from database for organisation {OrganisationId}", request.OrganisationId);

        var workItems = _issueRepository
            .GetByPlatformAsync(request.OrganisationId, IssuePlatform.AzureDevOps)
            .GetAwaiter()
            .GetResult()
            .ToList();

        return new QueryBuilder<Issue>(workItems)
            .Where(request.Id, p => p.Id.Value.EqualsCaseInsensitive(request.Id))
            .ToList();
    }
}