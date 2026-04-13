using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Requests;
using Orchitect.Domain.Inventory.SourceControl.Services;

namespace Orchitect.Api.Endpoints.Inventory.SourceControl;

public sealed class GetAllPullRequestsEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/", Handle)
        .WithSummary("Gets all pull requests by query parameters.");

    public sealed record GetAllPullRequestsResponse(
        List<GetPullRequestEndpoint.GetPullRequestResponse> PullRequests);

    public sealed record PullRequestQueryRequest(
        Guid OrganisationId,
        string? Id = null,
        string? Name = null,
        string? Description = null,
        string? Url = null,
        List<string>? Labels = null,
        PullRequestPlatform? Platform = null);

    private static Results<Ok<GetAllPullRequestsResponse>, NotFound> Handle(
        [AsParameters]
        PullRequestQueryRequest query,
        [FromServices]
        IPullRequestRepository repository)
    {
        var organisationId = new OrganisationId(query.OrganisationId);

        var pullRequestQuery = new PullRequestQuery(
            OrganisationId: organisationId,
            Id: query.Id,
            Name: query.Name,
            Description: query.Description,
            Url: query.Url,
            Labels: query.Labels,
            Platform: query.Platform);

        var pullRequests = repository.GetByQuery(pullRequestQuery);

        var response = pullRequests
            .Select(pr => new GetPullRequestEndpoint.GetPullRequestResponse(
                pr.Id.Value,
                pr.Name,
                pr.Description,
                pr.Url,
                pr.Labels,
                pr.Reviewers,
                pr.Status,
                pr.Platform,
                pr.RepositoryUrl,
                pr.RepositoryName,
                pr.CreatedOnDate,
                pr.DiscoveredAt,
                pr.UpdatedAt))
            .ToList();

        return TypedResults.Ok(new GetAllPullRequestsResponse(response));
    }
}
