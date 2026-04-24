using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Issue.Requests;
using Orchitect.Domain.Inventory.Issue.Services;

namespace Orchitect.Api.Endpoints.Inventory.Issue;

public sealed class GetAllIssuesEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Gets all issues by query parameters.");

    public sealed record GetAllIssuesResponse(
        List<GetIssueEndpoint.GetIssueResponse> Issues);

    public sealed record IssueQueryRequest(
        Guid OrganisationId,
        string? Id = null,
        string? Title = null);

    private static Results<Ok<GetAllIssuesResponse>, NotFound> Handle(
        [AsParameters]
        IssueQueryRequest query,
        [FromServices]
        IIssueRepository repository)
    {
        var organisationId = new OrganisationId(query.OrganisationId);

        var issueQuery = new IssueQuery(
            OrganisationId: organisationId,
            Id: query.Id,
            Title: query.Title);

        var issues = repository.GetByQuery(issueQuery);

        var response = issues
            .Select(i => new GetIssueEndpoint.GetIssueResponse(
                i.Id.Value,
                i.Title,
                i.Description,
                i.Url,
                i.Type,
                i.State,
                i.Platform,
                i.DiscoveredAt,
                i.UpdatedAt))
            .ToList();

        return TypedResults.Ok(new GetAllIssuesResponse(response));
    }
}
