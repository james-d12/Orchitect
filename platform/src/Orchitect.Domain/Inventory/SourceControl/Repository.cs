using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Identity;

namespace Orchitect.Domain.Inventory.SourceControl;

public record Repository : IEntity
{
    public required RepositoryId Id { get; init; }
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required Uri Url { get; init; }
    public required string DefaultBranch { get; init; }
    public required User User { get; init; }
    public required RepositoryPlatform Platform { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}