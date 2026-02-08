using Conductor.Inventory.Domain.Git;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Inventory.Persistence.Configurations;

internal sealed class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> builder)
    {
        builder.ToTable("Pipelines");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new PipelineId(value)
            );

        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Url).IsRequired();
        builder.Property(p => p.Platform).IsRequired().HasConversion<string>();

        builder.HasOne(p => p.Owner).WithMany().IsRequired();
    }
}
