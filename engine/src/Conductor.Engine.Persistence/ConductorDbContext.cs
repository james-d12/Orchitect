using Conductor.Engine.Domain.Application;
using Conductor.Engine.Domain.Deployment;
using Conductor.Engine.Domain.ResourceTemplate;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Environment = Conductor.Engine.Domain.Environment.Environment;

namespace Conductor.Engine.Persistence;

public sealed class ConductorDbContext : IdentityDbContext
{
    public required DbSet<ResourceTemplate> ResourceTemplates { get; init; }
    public required DbSet<Application> Applications { get; init; }
    public required DbSet<Environment> Environments { get; init; }
    public required DbSet<Deployment> Deployments { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "Conductor.db");

        if (!File.Exists(dbPath))
        {
            File.Create(dbPath);
        }

        optionsBuilder.UseSqlite("Data Source=" + dbPath);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        OverrideTableNamesForIdentity(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConductorDbContext).Assembly);
    }

    private void OverrideTableNamesForIdentity(ModelBuilder builder)
    {
        builder.Entity<IdentityUser>(b => { b.ToTable("Users"); });
        builder.Entity<IdentityRole>(b => { b.ToTable("Roles"); });
        builder.Entity<IdentityUserRole<string>>(b => { b.ToTable("UserRoles"); });
        builder.Entity<IdentityUserClaim<string>>(b => { b.ToTable("UserClaims"); });
        builder.Entity<IdentityRoleClaim<string>>(b => { b.ToTable("RoleClaims"); });
        builder.Entity<IdentityUserLogin<string>>(b => { b.ToTable("UserLogins"); });
        builder.Entity<IdentityUserToken<string>>(b => { b.ToTable("UserTokens"); });
    }
}