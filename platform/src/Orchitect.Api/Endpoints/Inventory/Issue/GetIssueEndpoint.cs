using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Inventory.Issue;
using Orchitect.Domain.Inventory.Issue.Services;

namespace Orchitect.Api.Endpoints.Inventory.Issue;

public sealed class GetIssueEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id}", HandleAsync)
        .WithSummary("Gets an issue by id.");

    public sealed record GetIssueResponse(
        string Id,
        string Title,
        string Description,
        Uri Url,
        string Type,
        string State,
        IssuePlatform Platform,
        DateTime DiscoveredAt,
        DateTime UpdatedAt);

    private static async Task<Results<Ok<GetIssueResponse>, NotFound>> HandleAsync(
        [FromRoute]
        string id,
        [FromServices]
        IIssueRepository repository,
        CancellationToken cancellationToken)
    {
        var issueId = new IssueId(id);
        var issue = await repository.GetByIdAsync(issueId, cancellationToken);

        if (issue is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetIssueResponse(
            issue.Id.Value,
            issue.Title,
            issue.Description,
            issue.Url,
            issue.Type,
            issue.State,
            issue.Platform,
            issue.DiscoveredAt,
            issue.UpdatedAt));
    }
}
