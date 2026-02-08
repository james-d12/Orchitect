using Conductor.Inventory.Domain.Cloud;
using Conductor.Inventory.Domain.Git;
using Conductor.Inventory.Domain.Ticketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Conductor.Inventory.Persistence;

public sealed class InventoryDbContext : DbContext
{
    public DbSet<CloudSecret> CloudSecrets { get; init; }
    public DbSet<CloudResource> CloudResources { get; init; }
    public DbSet<Owner> Owners { get; init; }
    public DbSet<Pipeline> Pipelines { get; init; }
    public DbSet<Repository> Repositories { get; init; }
    public DbSet<PullRequest> PullRequests { get; init; }
    public DbSet<User> TicketingUsers { get; init; }
    public DbSet<WorkItem> WorkItems { get; init; }

    private static readonly ILoggerFactory LoggerFactoryInstance
        = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information);
        });

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("inventory");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }
}
