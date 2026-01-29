using Conductor.Inventory.Domain.Cloud;
using Conductor.Inventory.Domain.Git;
using Conductor.Inventory.Domain.Ticketing;
using Microsoft.EntityFrameworkCore;

namespace Conductor.Inventory.Persistence;

public sealed class InventoryDbContext : DbContext
{
    public required DbSet<CloudSecret> CloudSecrets { get; init; }
    public required DbSet<CloudResource> CloudResources { get; init; }
    public required DbSet<Owner> Owners { get; init; }
    public required DbSet<Commit> Commits { get; init; }
    public required DbSet<Pipeline> Pipelines { get; init; }
    public required DbSet<Repository> Repositories { get; init; }
    public required DbSet<PullRequest> PullRequests { get; init; }
    public required DbSet<User> TicketingUsers { get; init; }
    public required DbSet<WorkItem> WorkItems { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ConductorInventory.db");

        if (!File.Exists(dbPath))
        {
            File.Create(dbPath).Close();
        }

        optionsBuilder.UseSqlite("Data Source=" + dbPath);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }
}
