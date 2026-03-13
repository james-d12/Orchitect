using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Inventory.Persistence.Repositories;

namespace Orchitect.Inventory.Persistence;

public static class InventoryPersistenceExtensions
{
    public static IServiceCollection AddInventoryPersistenceServices(this IServiceCollection services)
    {
        services.AddDbContext<InventoryDbContext>();
        services.AddScoped<IDiscoveryConfigurationRepository, DiscoveryConfigurationRepository>();
        return services;
    }

    public static async Task ApplyInventoryMigrations(this IServiceCollection services)
    {
        using IServiceScope scope = services.BuildServiceProvider().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}