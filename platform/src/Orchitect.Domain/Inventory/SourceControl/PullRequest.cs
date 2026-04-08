using System.Collections.Immutable;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.SourceControl;

public record PullRequest
{
    public required PullRequestId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Uri Url { get; init; }
    public required ImmutableHashSet<string> Labels { get; init; }
    public required ImmutableHashSet<string> Reviewers { get; init; }
    public required PullRequestStatus Status { get; init; }
    public required PullRequestPlatform Platform { get; init; }
    public required Commit? LastCommit { get; init; }
    public required Uri RepositoryUrl { get; init; }
    public required string RepositoryName { get; init; }
    public required DateOnly CreatedOnDate { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}