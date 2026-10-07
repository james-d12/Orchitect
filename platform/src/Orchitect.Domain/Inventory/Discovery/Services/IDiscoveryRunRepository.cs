using Orchitect.Domain.Core;

namespace Orchitect.Domain.Inventory.Discovery.Services;

public interface IDiscoveryRunRepository : IRepository<DiscoveryRun, DiscoveryRunId>
{
    /// <summary>
    /// Saves a run created earlier with its new status.
    /// </summary>
    Task<DiscoveryRun?> UpdateAsync(DiscoveryRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recently started run of a discovery configuration.
    /// </summary>
    Task<DiscoveryRun?> GetLatestAsync(DiscoveryConfigurationId configurationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recently started runs of a discovery configuration, newest first.
    /// </summary>
    Task<IReadOnlyList<DiscoveryRun>> GetRecentAsync(DiscoveryConfigurationId configurationId, int limit,
        CancellationToken cancellationToken = default);
}
