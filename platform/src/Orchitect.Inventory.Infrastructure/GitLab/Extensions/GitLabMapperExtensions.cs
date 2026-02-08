using System.Collections.Immutable;
using Orchitect.Inventory.Domain.Git;
using NGitLab.Models;
using Orchitect.Inventory.Infrastructure.GitLab.Models;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Commit = Orchitect.Inventory.Domain.Git.Commit;

namespace Orchitect.Inventory.Infrastructure.GitLab.Extensions;

public static class GitLabMapperExtensions
{
    public static GitLabPullRequest MapToGitLabPullRequest(this MergeRequest mergeRequest)
    {
        using var activity = Tracing.StartActivity();
        return new GitLabPullRequest
        {
            Id = new PullRequestId(mergeRequest.Id.ToString()),
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
            CreatedOnDate = DateOnly.FromDateTime(mergeRequest.CreatedAt)
        };
    }

    public static GitLabPipeline MapToGitLabPipeline(this PipelineBasic pipeline)
    {
        using var activity = Tracing.StartActivity();
        return new GitLabPipeline
        {
            Id = new PipelineId(pipeline.Id.ToString()),
            Name = pipeline.Name,
            Url = new Uri(pipeline.WebUrl),
            Owner = new Owner
            {
                Id = new OwnerId(string.Empty),
                Name = string.Empty,
                Description = string.Empty,
                Url = new Uri("https://gitlab.com"),
                Platform = OwnerPlatform.GitLab
            },
            Platform = PipelinePlatform.GitLab
        };
    }

    public static GitLabRepository MapToGitLabRepository(this Project project)
    {
        using var activity = Tracing.StartActivity();
        return new GitLabRepository
        {
            Id = new RepositoryId(project.Id.ToString()),
            Name = project.Name,
            Url = new Uri(project.WebUrl),
            DefaultBranch = project.DefaultBranch,
            Owner = new Owner
            {
                Id = new OwnerId(project.Owner.Id.ToString()),
                Name = project.Owner.Name,
                Description = project.Owner.Bio,
                Url = new Uri(project.Owner.WebURL),
                Platform = OwnerPlatform.GitLab
            },
            Platform = RepositoryPlatform.GitLab
        };
    }
}