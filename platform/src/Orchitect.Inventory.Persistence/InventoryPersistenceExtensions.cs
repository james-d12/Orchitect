using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Orchitect.Inventory.Persistence;

public static class InventoryPersistenceExtensions
{
    public static IServiceCollection AddInventoryPersistenceServices(this IServiceCollection services)
    {
        services.AddDbContext<InventoryDbContext>();
        return services;
    }

    public static async Task ApplyInventoryMigrations(this IServiceCollection services)
    {
        using IServiceScope scope = services.BuildServiceProvider().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}