using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Ticketing;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems", "inventory");

        builder.HasKey(wi => wi.Id);

        builder.Property(wi => wi.Id)
            .HasConversion(
                id => id.Value,
                value => new WorkItemId(value)
            );

        // Organisation FK with cascade delete
        builder.Property(wi => wi.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(wi => wi.OrganisationId)
            .HasConstraintName("FK_WorkItems_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(wi => wi.Title).IsRequired().HasMaxLength(500);
        builder.Property(wi => wi.Description).IsRequired();
        builder.Property(wi => wi.Url).IsRequired().HasMaxLength(2000);
        builder.Property(wi => wi.Type).IsRequired().HasMaxLength(100);
        builder.Property(wi => wi.State).IsRequired().HasMaxLength(100);
        builder.Property(wi => wi.Platform).IsRequired().HasConversion<string>();

        // Audit timestamps
        builder.Property(wi => wi.DiscoveredAt).IsRequired();
        builder.Property(wi => wi.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(wi => wi.OrganisationId)
            .HasDatabaseName("IX_WorkItems_OrganisationId");

        builder.HasIndex(wi => new { wi.OrganisationId, wi.Platform })
            .HasDatabaseName("IX_WorkItems_OrganisationId_Platform");
    }
}
