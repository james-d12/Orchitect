using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchitect.Core.Domain.Credential;
using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Core.Persistence;

public sealed class CoreDbContext : IdentityDbContext
{
    private const string Schema = "core";
    public DbSet<Organisation> Organisations { get; init; } = null!;
    public DbSet<Credential> Credentials { get; init; } = null!;

    private static readonly ILoggerFactory LoggerFactoryInstance
        = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information);
        });

    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__orchitect");
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        optionsBuilder
            .UseNpgsql(connectionString, opt => { opt.EnableRetryOnFailure(); })
            .UseLoggerFactory(LoggerFactoryInstance)
            .EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);
        OverrideTableNamesForIdentity(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
    }

    private static void OverrideTableNamesForIdentity(ModelBuilder builder)
    {
        builder.Entity<IdentityUser>(b => b.ToTable("Users", Schema));
        builder.Entity<IdentityRole>(b => b.ToTable("Roles", Schema));
        builder.Entity<IdentityUserRole<string>>(b => b.ToTable("UserRoles", Schema));
        builder.Entity<IdentityUserClaim<string>>(b => b.ToTable("UserClaims", Schema));
        builder.Entity<IdentityRoleClaim<string>>(b => b.ToTable("RoleClaims", Schema));
        builder.Entity<IdentityUserLogin<string>>(b => b.ToTable("UserLogins", Schema));
        builder.Entity<IdentityUserToken<string>>(b => b.ToTable("UserTokens", Schema));
    }
}