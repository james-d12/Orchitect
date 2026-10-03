using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.ResourceInstance;

namespace Orchitect.Persistence.Configurations.Engine;

internal sealed class DeploymentRunConfiguration : IEntityTypeConfiguration<DeploymentRun>
{
    public void Configure(EntityTypeBuilder<DeploymentRun> builder)
    {
        builder.ToTable("DeploymentRuns");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.DeploymentId, r.QueuedAt });

        builder.HasIndex(r => r.TokenHash)
            .IsUnique()
            .HasFilter("\"TokenHash\" IS NOT NULL");

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new DeploymentRunId(value)
            );

        builder.Property(r => r.DeploymentId)
            .HasConversion(
                id => id.Value,
                value => new DeploymentId(value)
            );

        builder.HasOne<Deployment>()
            .WithMany()
            .HasForeignKey(r => r.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.Operation).HasConversion<string>();
        builder.Property(r => r.Status).HasConversion<string>();
        builder.Property(r => r.QueuedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(r => r.ErrorSummary).HasMaxLength(DeploymentRun.ErrorSummaryMaxLength);
        builder.Property(r => r.RunnerId).HasMaxLength(DeploymentRun.RunnerIdMaxLength);
        builder.Property(r => r.LogLocation).HasMaxLength(DeploymentRun.LogLocationMaxLength);
        builder.Property(r => r.TokenHash).HasMaxLength(DeploymentRun.TokenHashMaxLength);
        builder.Property(r => r.ProjectName).HasMaxLength(DeploymentRun.ProjectNameMaxLength);

        builder.PrimitiveCollection(r => r.InstanceIds)
            .IsRequired()
            .ElementType()
            .HasConversion<ResourceInstanceIdConverter>();
    }

    private sealed class ResourceInstanceIdConverter() : ValueConverter<ResourceInstanceId, Guid>(
        id => id.Value,
        value => new ResourceInstanceId(value));
}
