using Conductor.Inventory.Domain.Ticketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Inventory.Persistence.Configurations;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");

        builder.HasKey(wi => wi.Id);

        builder.Property(wi => wi.Id)
            .HasConversion(
                id => id.Value,
                value => new WorkItemId(value)
            );

        builder.Property(wi => wi.Title).IsRequired();
        builder.Property(wi => wi.Description).IsRequired();
        builder.Property(wi => wi.Url).IsRequired();
        builder.Property(wi => wi.Type).IsRequired();
        builder.Property(wi => wi.State).IsRequired();
        builder.Property(wi => wi.Platform).IsRequired().HasConversion<string>();
    }
}
