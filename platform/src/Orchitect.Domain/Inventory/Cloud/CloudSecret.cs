using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Cloud;

public enum CloudSecretPlatform
{
    Azure,
    Aws,
    GoogleCloud,
}

public sealed record CloudSecret
{
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required string Location { get; init; }
    public required Uri Url { get; init; }
    public required CloudSecretPlatform Platform { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}