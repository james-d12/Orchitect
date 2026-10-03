using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orchitect.Domain.Engine.Deployment;
using Orchitect.Domain.Engine.ResourceInstance;

namespace Orchitect.Persistence.Configurations.Engine;

internal sealed class DeploymentRunPlanConfiguration : IEntityTypeConfiguration<DeploymentRunPlan>
{
    public void Configure(EntityTypeBuilder<DeploymentRunPlan> builder)
    {
        builder.ToTable("DeploymentRunPlans");

        builder.HasKey(p => p.RunId);

        builder.Property(p => p.RunId)
            .HasConversion(
                id => id.Value,
                value => new DeploymentRunId(value)
            );

        builder.HasOne<DeploymentRun>()
            .WithOne()
            .HasForeignKey<DeploymentRunPlan>(p => p.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Contents)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.Instances)
            .HasConversion(new PlannedInstancesConverter(), new PlannedInstancesComparer())
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
    }

    private sealed record StoredInstance(Guid InstanceId, Uri? Location, string? Workspace);

    private sealed class PlannedInstancesConverter()
        : ValueConverter<IReadOnlyList<PlannedResourceInstance>, string>(
            v => Serialize(v),
            v => Deserialize(v))
    {
        private static string Serialize(IReadOnlyList<PlannedResourceInstance> instances) =>
            JsonSerializer.Serialize(instances
                .Select(i => new StoredInstance(i.InstanceId.Value, i.Output?.Location, i.Output?.Workspace))
                .ToList());

        private static IReadOnlyList<PlannedResourceInstance> Deserialize(string json) =>
            (JsonSerializer.Deserialize<List<StoredInstance>>(json) ?? [])
            .Select(i => new PlannedResourceInstance(new ResourceInstanceId(i.InstanceId),
                i.Location is null ? null : new ResourceInstanceOutput { Location = i.Location, Workspace = i.Workspace }))
            .ToList();
    }

    private sealed class PlannedInstancesComparer()
        : ValueComparer<IReadOnlyList<PlannedResourceInstance>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, i) => HashCode.Combine(hash, i.GetHashCode())),
            v => v.ToList());
}
