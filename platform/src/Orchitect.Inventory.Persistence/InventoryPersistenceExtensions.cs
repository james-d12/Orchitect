using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Orchitect.Inventory.Persistence;

public static class InventoryPersistenceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInventoryPersistenceServices()
        {
            services.AddDbContext<InventoryDbContext>();
            return services;
        }

        public async Task ApplyInventoryMigrations()
        {
            using IServiceScope scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }
}