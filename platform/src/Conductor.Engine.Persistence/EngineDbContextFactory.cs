using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Conductor.Engine.Persistence;

[ExcludeFromCodeCoverage]
public sealed class EngineDbContextFactory : IDesignTimeDbContextFactory<EngineDbContext>
{
    public EngineDbContext CreateDbContext(string[] args)
    {
        var host = "localhost";
        var port = "5432";
        var db = "conductor";
        var user = "admin";
        var password = "example";

        var optionsBuilder = new DbContextOptionsBuilder<EngineDbContext>();
        optionsBuilder.UseNpgsql(
            $"Host={host};Port={port};Database={db};Username={user};Password={password}",
            opt => opt.EnableRetryOnFailure()
        );

        return new EngineDbContext(optionsBuilder.Options);
    }
}