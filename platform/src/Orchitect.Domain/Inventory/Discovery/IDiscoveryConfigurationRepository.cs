using Orchitect.Domain.Core;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Discovery;

public interface IDiscoveryConfigurationRepository : IRepository<DiscoveryConfiguration, DiscoveryConfigurationId>
{
    Task<IEnumerable<DiscoveryConfiguration>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DiscoveryConfiguration>> GetEnabledConfigurationsAsync(
        CancellationToken cancellationToken = default);

    Task<DiscoveryConfiguration?> UpdateAsync(
        DiscoveryConfiguration configuration,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        DiscoveryConfigurationId id,
        CancellationToken cancellationToken = default);
}
