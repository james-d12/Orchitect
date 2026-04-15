using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Orchitect.Api.Shared;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Domain.Inventory.SourceControl.Services;

namespace Orchitect.Api.Endpoints.Inventory.SourceControl;

public sealed class GetPullRequestEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapGet("/{id}", HandleAsync)
        .WithSummary("Gets a pull request by id.");

    public sealed record GetPullRequestResponse(
        string Id,
        string Name,
        string Description,
        Uri Url,
        List<string> Labels,
        List<string> Reviewers,
        PullRequestStatus Status,
        PullRequestPlatform Platform,
        Uri RepositoryUrl,
        string RepositoryName,
        DateOnly CreatedOnDate,
        DateTime DiscoveredAt,
        DateTime UpdatedAt);

    private static async Task<Results<Ok<GetPullRequestResponse>, NotFound>> HandleAsync(
        [FromRoute]
        string id,
        [FromServices]
        IPullRequestRepository repository,
        CancellationToken cancellationToken)
    {
        var pullRequestId = new PullRequestId(id);
        var pullRequest = await repository.GetByIdAsync(pullRequestId, cancellationToken);

        if (pullRequest is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetPullRequestResponse(
            pullRequest.Id.Value,
            pullRequest.Name,
            pullRequest.Description,
            pullRequest.Url,
            pullRequest.Labels.ToList(),
            pullRequest.Reviewers.ToList(),
            pullRequest.Status,
            pullRequest.Platform,
            pullRequest.RepositoryUrl,
            pullRequest.RepositoryName,
            pullRequest.CreatedOnDate,
            pullRequest.DiscoveredAt,
            pullRequest.UpdatedAt));
    }
}
