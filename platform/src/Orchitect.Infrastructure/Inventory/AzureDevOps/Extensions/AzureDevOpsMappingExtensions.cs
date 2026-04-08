using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Identity;
using Orchitect.Domain.Inventory.Issue;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.SourceControl;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Models;
using Orchitect.Infrastructure.Inventory.Shared.Observability;
using PullRequestStatus = Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus;
using WorkItem = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Extensions;

public static class AzureDevOpsMappingExtensions
{
    public static AzureDevOpsPipeline MapToAzureDevOpsPipeline(this BuildDefinitionReference buildDefinitionReference,
        Uri projectUri,
        OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();

        var url = new Uri($"{projectUri}/_build?definitionId={buildDefinitionReference.Id}");
        var now = DateTime.UtcNow;

        return new AzureDevOpsPipeline
        {
            Id = new PipelineId(buildDefinitionReference.Id.ToString()),
            OrganisationId = organisationId,
            Name = buildDefinitionReference.Name,
            Url = url,
            Path = buildDefinitionReference.Path,
            Platform = PipelinePlatform.AzureDevOps,
            User = new User
            {
                Id = new UserId(buildDefinitionReference.Project.Id.ToString()),
                OrganisationId = organisationId,
                Name = buildDefinitionReference.Project.Name,
                Description = buildDefinitionReference.Project.Description,
                Url = new Uri(buildDefinitionReference.Project.Url.Replace("_apis/", string.Empty)
                    .Replace("projects/", string.Empty)),
                Platform = UserPlatform.AzureDevOps,
                DiscoveredAt = now,
                UpdatedAt = now
            },
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }

    public static AzureDevOpsProject MapToAzureDevOpsProject(this TeamProjectReference teamProjectReference,
        string organisation)
    {
        using var activity = Tracing.StartActivity();
        var teamProject = new TeamProject(teamProjectReference);

        return new AzureDevOpsProject
        {
            Id = teamProjectReference.Id,
            Name = teamProject.Name,
            Description = teamProject.Description,
            Url = new Uri($"https://dev.azure.com/{organisation}/{teamProject.Name}"),
        };
    }

    public static AzureDevOpsRepository MapToAzureDevOpsRepository(this GitRepository gitRepository, OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();
        var now = DateTime.UtcNow;
        return new AzureDevOpsRepository
        {
            Id = new RepositoryId(gitRepository.Id.ToString()),
            OrganisationId = organisationId,
            Name = gitRepository.Name,
            Url = new Uri(gitRepository.WebUrl),
            DefaultBranch = gitRepository.DefaultBranch?.Replace("refs/heads/", string.Empty) ?? string.Empty,
            IsDisabled = gitRepository.IsDisabled ?? false,
            IsInMaintenance = gitRepository.IsInMaintenance ?? false,
            Platform = RepositoryPlatform.AzureDevOps,
            User = new User
            {
                Id = new UserId(gitRepository.ProjectReference.Id.ToString()),
                OrganisationId = organisationId,
                Name = gitRepository.ProjectReference.Name,
                Description = gitRepository.ProjectReference.Description,
                Url = new Uri(gitRepository.ProjectReference.Url.Replace("_apis/", string.Empty)
                    .Replace("projects/", string.Empty)),
                Platform = UserPlatform.AzureDevOps,
                DiscoveredAt = now,
                UpdatedAt = now
            },
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }

    public static AzureDevOpsTeam MapToAzureDevOpsTeam(this WebApiTeam webApiTeam)
    {
        using var activity = Tracing.StartActivity();
        return new AzureDevOpsTeam
        {
            Id = new TeamId(webApiTeam.Id.ToString()),
            Name = webApiTeam.Name,
            Description = webApiTeam.Description,
            Url = new Uri(webApiTeam.Url),
            OrganisationId = default,
            Platform = TeamPlatform.AzureDevOps,
            DiscoveredAt = default,
            UpdatedAt = default
        };
    }

    public static Team MapToDomainTeam(this WebApiTeam webApiTeam, OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();
        var now = DateTime.UtcNow;
        return new Team
        {
            Id = new TeamId(webApiTeam.Id.ToString()),
            OrganisationId = organisationId,
            Name = webApiTeam.Name,
            Description = webApiTeam.Description,
            Url = new Uri(webApiTeam.Url),
            Platform = TeamPlatform.AzureDevOps,
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }

    public static AzureDevOpsPullRequest MapToAzureDevOpsPullRequest(this GitPullRequest gitPullRequest,
        Uri projectUri,
        OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();
        var status = gitPullRequest.Status switch
        {
            PullRequestStatus.NotSet => Domain.Inventory.SourceControl.PullRequestStatus.Draft,
            PullRequestStatus.Active => Domain.Inventory.SourceControl.PullRequestStatus.Active,
            PullRequestStatus.Abandoned => Domain.Inventory.SourceControl.PullRequestStatus.Abandoned,
            PullRequestStatus.Completed => Domain.Inventory.SourceControl.PullRequestStatus.Completed,
            _ => Domain.Inventory.SourceControl.PullRequestStatus.Unknown
        };

        var url = $"{projectUri}/_git/{gitPullRequest.Repository.Name}/pullrequest/{gitPullRequest.PullRequestId}";
        var repoUrl = $"{projectUri}/_git/{gitPullRequest.Repository.Name}";
        var now = DateTime.UtcNow;

        return new AzureDevOpsPullRequest
        {
            Id = new PullRequestId(gitPullRequest.PullRequestId.ToString()),
            OrganisationId = organisationId,
            Name = gitPullRequest.Title,
            Description = gitPullRequest.Description,
            Url = new Uri(url),
            Labels = gitPullRequest.Labels?.Select(l => l.Name).ToImmutableHashSet() ?? [],
            Reviewers = gitPullRequest.Reviewers?.Select(r => r.DisplayName).ToImmutableHashSet() ?? [],
            Status = status,
            Platform = PullRequestPlatform.AzureDevOps,
            LastCommit = new Commit
            {
                Id = new CommitId(gitPullRequest.LastMergeCommit?.CommitId ?? string.Empty),
                Url = new Uri(gitPullRequest.LastMergeCommit?.Url ?? "https://dev.azure.com"),
                Committer = gitPullRequest.LastMergeCommit?.Committer?.Name ?? string.Empty,
                Comment = gitPullRequest.LastMergeCommit?.Comment ?? string.Empty,
                ChangeCount = gitPullRequest.LastMergeCommit?.ChangeCounts?.Count ?? 0
            },
            RepositoryName = gitPullRequest.Repository?.Name ?? string.Empty,
            RepositoryUrl = new Uri(repoUrl),
            CreatedOnDate = DateOnly.FromDateTime(gitPullRequest.CreationDate),
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }

    public static AzureDevOpsIssue MapToAzureDevOpsWorkItem(this WorkItem workItem, Uri projectUri, OrganisationId organisationId)
    {
        using var activity = Tracing.StartActivity();

        var url = $"{projectUri}/_workitems/edit/{workItem.Id}";
        var now = DateTime.UtcNow;

        return new AzureDevOpsIssue
        {
            Id = new IssueId(workItem.Id?.ToString() ?? string.Empty),
            OrganisationId = organisationId,
            Title = workItem.Fields["System.Title"]?.ToString() ?? string.Empty,
            Description = string.Empty,
            Type = workItem.Fields["System.WorkItemType"]?.ToString() ?? string.Empty,
            State = workItem.Fields["System.State"]?.ToString() ?? string.Empty,
            Url = new Uri(url),
            Revision = workItem.Rev ?? 0,
            Fields = workItem.Fields?.ToFrozenDictionary() ?? FrozenDictionary<string, object>.Empty,
            Relations = workItem.Relations?.Select(r => r.Title)
                            .ToImmutableHashSet() ??
                        [],
            Platform = IssuePlatform.AzureDevOps,
            DiscoveredAt = now,
            UpdatedAt = now
        };
    }
}