using Orchitect.Inventory.Domain.Cloud;
using Orchitect.Inventory.Domain.Git;
using Orchitect.Inventory.Domain.Ticketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Orchitect.Inventory.Persistence;

public sealed class InventoryDbContext : DbContext
{
    public DbSet<CloudSecret> CloudSecrets { get; init; } = null!;
    public DbSet<CloudResource> CloudResources { get; init; } = null!;
    public DbSet<Owner> Owners { get; init; } = null!;
    public DbSet<Pipeline> Pipelines { get; init; } = null!;
    public DbSet<Repository> Repositories { get; init; } = null!;
    public DbSet<PullRequest> PullRequests { get; init; } = null!;
    public DbSet<User> TicketingUsers { get; init; } = null!;
    public DbSet<WorkItem> WorkItems { get; init; } = null!;

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

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__orchitect");
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