using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Inventory.Discovery;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class DiscoveryRunConfiguration : IEntityTypeConfiguration<DiscoveryRun>
{
    public void Configure(EntityTypeBuilder<DiscoveryRun> builder)
    {
        builder.ToTable("DiscoveryRuns", "inventory");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.DiscoveryConfigurationId, r.StartedAt });

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new DiscoveryRunId(value));

        builder.Property(r => r.DiscoveryConfigurationId)
            .HasConversion(
                id => id.Value,
                value => new DiscoveryConfigurationId(value))
            .IsRequired();

        builder.HasOne<DiscoveryConfiguration>()
            .WithMany()
            .HasForeignKey(r => r.DiscoveryConfigurationId)
            .HasConstraintName("FK_DiscoveryRuns_DiscoveryConfigurations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.Status).HasConversion<string>().IsRequired();
        builder.Property(r => r.StartedAt).IsRequired();
        builder.Property(r => r.ErrorMessage).HasMaxLength(DiscoveryRun.ErrorMessageMaxLength);

        builder.OwnsOne(r => r.Counts, c =>
        {
            c.Property(x => x.Teams).HasColumnName("TeamCount");
            c.Property(x => x.Repositories).HasColumnName("RepositoryCount");
            c.Property(x => x.Pipelines).HasColumnName("PipelineCount");
            c.Property(x => x.PullRequests).HasColumnName("PullRequestCount");
            c.Property(x => x.Issues).HasColumnName("IssueCount");
            c.Property(x => x.CloudResources).HasColumnName("CloudResourceCount");
            c.Property(x => x.CloudSecrets).HasColumnName("CloudSecretCount");
        });

        builder.Navigation(r => r.Counts).IsRequired();
    }
}
