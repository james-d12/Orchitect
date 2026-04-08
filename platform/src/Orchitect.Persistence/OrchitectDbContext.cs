using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Domain.Inventory.Cloud;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Identity;
using Orchitect.Domain.Inventory.Issue;
using Orchitect.Domain.Inventory.Pipeline;
using Orchitect.Domain.Inventory.SourceControl;

namespace Orchitect.Persistence;

public sealed class OrchitectDbContext : IdentityDbContext
{
    public DbSet<CloudSecret> CloudSecrets { get; init; } = null!;
    public DbSet<CloudResource> CloudResources { get; init; } = null!;
    public DbSet<DiscoveryConfiguration> DiscoveryConfigurations { get; init; } = null!;
    public DbSet<User> Owners { get; init; } = null!;
    public DbSet<Pipeline> Pipelines { get; init; } = null!;
    public DbSet<Repository> Repositories { get; init; } = null!;
    public DbSet<PullRequest> PullRequests { get; init; } = null!;
    public DbSet<Team> Teams { get; init; } = null!;
    public DbSet<Issue> Issues { get; init; } = null!;

    public DbSet<ResourceTemplate> ResourceTemplates { get; init; } = null!;
    public DbSet<Domain.Engine.Application.Application> Applications { get; init; } = null!;
    public DbSet<Domain.Engine.Environment.Environment> Environments { get; init; } = null!;
    public DbSet<Deployment> Deployments { get; init; } = null!;
    public DbSet<Resource> Resources { get; init; } = null!;

    public DbSet<Organisation> Organisations { get; init; } = null!;
    public DbSet<Credential> Credentials { get; init; } = null!;

    private static readonly ILoggerFactory LoggerFactoryInstance
        = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information);
        });

    public OrchitectDbContext(DbContextOptions<OrchitectDbContext> options) : base(options)
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
            .UseNpgsql(connectionString, opt => opt.EnableRetryOnFailure())
            .UseLoggerFactory(LoggerFactoryInstance)
            .EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        OverrideTableNamesForIdentity(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(OrchitectDbContext).Assembly);
    }

    private static void OverrideTableNamesForIdentity(ModelBuilder builder)
    {
        builder.Entity<IdentityUser>(b => b.ToTable("Users"));
        builder.Entity<IdentityRole>(b => b.ToTable("Roles"));
        builder.Entity<IdentityUserRole<string>>(b => b.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<string>>(b => b.ToTable("UserClaims"));
        builder.Entity<IdentityRoleClaim<string>>(b => b.ToTable("RoleClaims"));
        builder.Entity<IdentityUserLogin<string>>(b => b.ToTable("UserLogins"));
        builder.Entity<IdentityUserToken<string>>(b => b.ToTable("UserTokens"));
    }
}