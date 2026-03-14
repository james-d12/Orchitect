using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Cloud;

public enum CloudPlatform
{
    Azure,
    Aws,
    GoogleCloud
}

public readonly record struct CloudResourceId(string Value);

public record CloudResource
{
    public required CloudResourceId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Uri Url { get; init; }
    public required string Type { get; init; }
    public required CloudPlatform Platform { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}