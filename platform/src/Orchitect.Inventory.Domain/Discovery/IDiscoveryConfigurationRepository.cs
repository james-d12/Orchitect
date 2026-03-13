using Orchitect.Core.Domain;
using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Inventory.Domain.Discovery;

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
