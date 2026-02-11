using Orchitect.Engine.Domain.Application;
using Orchitect.Engine.Domain.Deployment;
using Orchitect.Engine.Domain.Resource;
using Orchitect.Engine.Domain.ResourceTemplate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Orchitect.Engine.Persistence;

public sealed class EngineDbContext : DbContext
{
    private const string Schema = "engine";
    public DbSet<ResourceTemplate> ResourceTemplates { get; init; } = null!;
    public DbSet<Application> Applications { get; init; } = null!;
    public DbSet<Orchitect.Engine.Domain.Environment.Environment> Environments { get; init; } = null!;
    public DbSet<Deployment> Deployments { get; init; } = null!;
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
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EngineDbContext).Assembly);
    }
}