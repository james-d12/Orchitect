using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orchitect.Core.Persistence;

[ExcludeFromCodeCoverage]
public sealed class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var host = "localhost";
        var port = "5432";
        var db = "orchitect";
        var user = "admin";
        var password = "example";

        var optionsBuilder = new DbContextOptionsBuilder<CoreDbContext>();
        optionsBuilder.UseNpgsql(
            $"Host={host};Port={port};Database={db};Username={user};Password={password}",
            opt => opt.EnableRetryOnFailure()
        );

        return new CoreDbContext(optionsBuilder.Options);
    }
}