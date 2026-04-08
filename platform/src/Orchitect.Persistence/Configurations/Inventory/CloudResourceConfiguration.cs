using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class CloudResourceConfiguration : IEntityTypeConfiguration<CloudResource>
{
    public void Configure(EntityTypeBuilder<CloudResource> builder)
    {
        builder.ToTable("CloudResources", "inventory");

        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.Id)
            .HasConversion(
                id => id.Value,
                value => new CloudResourceId(value)
            );

        builder.Property(cr => cr.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(cr => cr.OrganisationId)
            .HasConstraintName("FK_CloudResources_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(cr => cr.Name).IsRequired().HasMaxLength(500);
        builder.Property(cr => cr.Description).IsRequired();
        builder.Property(cr => cr.Url).IsRequired().HasMaxLength(2000);
        builder.Property(cr => cr.Type).IsRequired().HasMaxLength(200);
        builder.Property(cr => cr.Platform).IsRequired().HasConversion<string>();

        // Audit timestamps
        builder.Property(cr => cr.DiscoveredAt).IsRequired();
        builder.Property(cr => cr.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(cr => cr.OrganisationId)
            .HasDatabaseName("IX_CloudResources_OrganisationId");

        builder.HasIndex(cr => new { cr.OrganisationId, cr.Platform })
            .HasDatabaseName("IX_CloudResources_OrganisationId_Platform");
    }
}