using Conductor.Engine.Domain.Application;
using Conductor.Engine.Domain.Deployment;
using Conductor.Engine.Domain.Organisation;
using Conductor.Engine.Domain.Resource;
using Conductor.Engine.Domain.ResourceTemplate;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Conductor.Engine.Persistence;

public sealed class EngineDbContext : IdentityDbContext
{
    public DbSet<ResourceTemplate> ResourceTemplates { get; init; } = null!;
    public DbSet<Application> Applications { get; init; } = null!;
    public DbSet<Conductor.Engine.Domain.Environment.Environment> Environments { get; init; } = null!;
    public DbSet<Deployment> Deployments { get; init; } = null!;
    public DbSet<Organisation> Organisations { get; init; } = null!;
    public DbSet<Resource> Resources { get; init; } = null!;

    private static readonly ILoggerFactory LoggerFactoryInstance
        = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information);
        });

    public EngineDbContext(DbContextOptions<EngineDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__conductor");
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        optionsBuilder
            .UseNpgsql(connectionString, opt => { opt.EnableRetryOnFailure(); })
            .UseLoggerFactory(LoggerFactoryInstance)
            .EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        OverrideTableNamesForIdentity(builder);
        builder.HasDefaultSchema("engine");
        builder.ApplyConfigurationsFromAssembly(typeof(EngineDbContext).Assembly);
    }

    private static void OverrideTableNamesForIdentity(ModelBuilder builder)
    {
        const string identitySchema = "identity";

        builder.Entity<IdentityUser>(b => b.ToTable("Users", identitySchema));
        builder.Entity<IdentityRole>(b => b.ToTable("Roles", identitySchema));
        builder.Entity<IdentityUserRole<string>>(b => b.ToTable("UserRoles", identitySchema));
        builder.Entity<IdentityUserClaim<string>>(b => b.ToTable("UserClaims", identitySchema));
        builder.Entity<IdentityRoleClaim<string>>(b => b.ToTable("RoleClaims", identitySchema));
        builder.Entity<IdentityUserLogin<string>>(b => b.ToTable("UserLogins", identitySchema));
        builder.Entity<IdentityUserToken<string>>(b => b.ToTable("UserTokens", identitySchema));
    }
}