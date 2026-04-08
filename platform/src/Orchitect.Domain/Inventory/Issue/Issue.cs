using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Issue;

public record Issue
{
    public required IssueId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required Uri Url { get; init; }
    public required string Type { get; init; }
    public required string State { get; init; }
    public required IssuePlatform Platform { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}