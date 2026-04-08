using Orchitect.Domain.Inventory.Issue.Requests;

namespace Orchitect.Domain.Inventory.Issue.Services;

public interface IIssueQueryService
{
    List<Issue> QueryWorkItems(IssueQueryRequest request);
}