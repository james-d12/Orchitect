using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Conductor.Inventory.Persistence;

[ExcludeFromCodeCoverage]
public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var host = "localhost";
        var port = "5432";
        var db = "conductor";
        var user = "admin";
        var password = "example";

        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseNpgsql(
            $"Host={host};Port={port};Database={db};Username={user};Password={password}",
            opt => opt.EnableRetryOnFailure()
        );

        return new InventoryDbContext(optionsBuilder.Options);
    }
}