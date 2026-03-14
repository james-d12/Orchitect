using System.Collections.Immutable;
using NGitLab.Models;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Git;
using Orchitect.Infrastructure.Inventory.GitLab.Models;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using Commit = Orchitect.Domain.Inventory.Git.Commit;

namespace Orchitect.Infrastructure.Inventory.GitLab.Extensions;

public static class GitLabMapperExtensions
{
    public static GitLabPullRequest MapToGitLabPullRequest(this MergeRequest mergeRequest, OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();
        var now = DateTime.UtcNow;
        return new GitLabPullRequest
        {
            Id = new PullRequestId(mergeRequest.Id.ToString()),
            OrganisationId = organisationId,
            Name = mergeRequest.Title,
            Description = mergeRequest.Description,
            Url = new Uri(mergeRequest.WebUrl),
            Labels = mergeRequest.Labels.ToImmutableHashSet(),
            Reviewers = mergeRequest.Reviewers.Select(r => r.Name).ToImmutableHashSet(),
            Status = PullRequestStatus.Draft,
            Platform = PullRequestPlatform.GitLab,
            LastCommit = new Commit
            {
                Id = new CommitId(""),
                Url = new Uri(mergeRequest.WebUrl),
                Committer = string.Empty,
                Comment = string.Empty,
                ChangeCount = 0
            },
            RepositoryUrl = new Uri(mergeRequest.WebUrl),
            RepositoryName = string.Empty,
            CreatedOnDate = DateOnly.FromDateTime(mergeRequest.CreatedAt),
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }

    public static GitLabPipeline MapToGitLabPipeline(this PipelineBasic pipeline, OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();
        var now = DateTime.UtcNow;
        return new GitLabPipeline
        {
            Id = new PipelineId(pipeline.Id.ToString()),
            OrganisationId = organisationId,
            Name = pipeline.Name,
            Url = new Uri(pipeline.WebUrl),
            Owner = new Owner
            {
                Id = new OwnerId(string.Empty),
                OrganisationId = organisationId,
                Name = string.Empty,
                Description = string.Empty,
                Url = new Uri("https://gitlab.com"),
                Platform = OwnerPlatform.GitLab,
                DiscoveredAt = now,
                UpdatedAt = now
            },
            Platform = PipelinePlatform.GitLab,
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }

    public static GitLabRepository MapToGitLabRepository(this Project project, OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();
        var now = DateTime.UtcNow;
        return new GitLabRepository
        {
            Id = new RepositoryId(project.Id.ToString()),
            OrganisationId = organisationId,
            Name = project.Name,
            Url = new Uri(project.WebUrl),
            DefaultBranch = project.DefaultBranch,
            Owner = new Owner
            {
                Id = new OwnerId(project.Owner.Id.ToString()),
                OrganisationId = organisationId,
                Name = project.Owner.Name,
                Description = project.Owner.Bio,
                Url = new Uri(project.Owner.WebURL),
                Platform = OwnerPlatform.GitLab,
                DiscoveredAt = now,
                UpdatedAt = now
            },
            Platform = RepositoryPlatform.GitLab,
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }
}