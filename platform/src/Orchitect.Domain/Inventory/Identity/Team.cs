using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Identity;

public record Team
{
    public required TeamId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required string? Description { get; init; }
    public required Uri Url { get; init; }
    public required TeamPlatform Platform { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
