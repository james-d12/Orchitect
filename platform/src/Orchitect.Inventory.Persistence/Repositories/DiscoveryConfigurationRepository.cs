using Microsoft.EntityFrameworkCore;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Inventory.Persistence.Repositories;

public sealed class DiscoveryConfigurationRepository : IDiscoveryConfigurationRepository
{
    private readonly InventoryDbContext _context;

    public DiscoveryConfigurationRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public IEnumerable<DiscoveryConfiguration> GetAll()
    {
        return _context.DiscoveryConfigurations
            .OrderBy(x => x.OrganisationId)
            .ThenBy(x => x.Platform)
            .ToList();
    }

    public async Task<DiscoveryConfiguration?> GetByIdAsync(
        DiscoveryConfigurationId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.DiscoveryConfigurations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<DiscoveryConfiguration>> GetByOrganisationIdAsync(
        OrganisationId organisationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DiscoveryConfigurations
            .Where(x => x.OrganisationId == organisationId)
            .OrderBy(x => x.Platform)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DiscoveryConfiguration>> GetEnabledConfigurationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.DiscoveryConfigurations
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.OrganisationId)
            .ThenBy(x => x.Platform)
            .ToListAsync(cancellationToken);
    }

    public async Task<DiscoveryConfiguration?> CreateAsync(
        DiscoveryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await _context.DiscoveryConfigurations.AddAsync(configuration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return configuration;
    }

    public async Task<DiscoveryConfiguration?> UpdateAsync(
        DiscoveryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _context.DiscoveryConfigurations.Update(configuration);
        await _context.SaveChangesAsync(cancellationToken);
        return configuration;
    }

    public async Task<bool> DeleteAsync(
        DiscoveryConfigurationId id,
        CancellationToken cancellationToken = default)
    {
        var configuration = await GetByIdAsync(id, cancellationToken);
        if (configuration == null) return false;

        _context.DiscoveryConfigurations.Remove(configuration);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
