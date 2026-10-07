using Orchitect.Domain.Inventory.Discovery;

namespace Orchitect.Api.Endpoints.Inventory.Discovery;

public sealed record DiscoveryRunResponse(
    Guid Id,
    Guid DiscoveryConfigurationId,
    DiscoveryRunStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DiscoveryCounts Counts,
    string? ErrorMessage)
{
    public static DiscoveryRunResponse From(DiscoveryRun run) => new(
        run.Id.Value,
        run.DiscoveryConfigurationId.Value,
        run.Status,
        run.StartedAt,
        run.CompletedAt,
        run.Counts,
        run.ErrorMessage);
}
