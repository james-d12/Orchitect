using System.Collections.Immutable;
using Octokit;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Git;
using Orchitect.Infrastructure.Inventory.GitHub.Models;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.GitHub.Extensions;

public static class GitHubMapperExtensions
{
    public static GitHubRepository MapToGitHubRepository(this Octokit.Repository repository, OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();
        var now = DateTime.UtcNow;
        return new GitHubRepository
        {
            Id = new RepositoryId(repository.Id.ToString()),
            OrganisationId = organisationId,
            Name = repository.Name,
            Url = new Uri(repository.HtmlUrl),
            DefaultBranch = repository.DefaultBranch,
            Owner = new Owner
            {
                Id = new OwnerId(repository.Owner.Id.ToString()),
                OrganisationId = organisationId,
                Name = repository.Owner.Login,
                Description = repository.Owner.Bio,
                Url = new Uri(repository.Owner.HtmlUrl),
                Platform = OwnerPlatform.GitHub,
                DiscoveredAt = now,
                UpdatedAt = now
            },
            Platform = RepositoryPlatform.GitHub,
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }

    public static GitHubPipeline MapToGitHubPipeline(this Workflow workflow, GitHubRepository repository)
    {
        using var activity = Tracing.StartActivity();
        var name = new Uri(workflow.HtmlUrl).Segments[^1];
        var fullUrl = $"{repository.Url}/actions/workflows/{name}";
        var now = DateTime.UtcNow;

        return new GitHubPipeline
        {
            Id = new PipelineId(workflow.Id.ToString()),
            OrganisationId = repository.OrganisationId,
            Name = $"{repository.Name}-{workflow.Name}",
            Url = new Uri(fullUrl),
            Owner = repository.Owner,
            Platform = PipelinePlatform.GitHub,
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }

    public static GitHubPullRequest MapToGitHubPullRequest(this Octokit.PullRequest pullRequest,
        GitHubRepository repository)
    {
        using var activity = Tracing.StartActivity();
        var status = pullRequest.State.Value switch
        {
            ItemState.Open => PullRequestStatus.Active,
            ItemState.Closed => PullRequestStatus.Completed,
            _ => PullRequestStatus.Unknown
        };
        var now = DateTime.UtcNow;

        return new GitHubPullRequest
        {
            Id = new PullRequestId(pullRequest.Id.ToString()),
            OrganisationId = repository.OrganisationId,
            Name = pullRequest.Title,
            Description = pullRequest.Title,
            Url = new Uri(pullRequest.HtmlUrl),
            Labels = pullRequest.Labels.Select(l => l.Name).ToImmutableHashSet(),
            Reviewers = pullRequest.RequestedReviewers.Select(r => r.Name).ToImmutableHashSet(),
            Status = status,
            Platform = PullRequestPlatform.GitHub,
            LastCommit = null,
            RepositoryUrl = repository.Url,
            RepositoryName = repository.Name,
            CreatedOnDate = DateOnly.FromDateTime(pullRequest.CreatedAt.UtcDateTime),
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }
}