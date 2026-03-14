using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Git;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> builder)
    {
        builder.ToTable("Owners", "inventory");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => new OwnerId(value)
            );

        // Organisation FK with cascade delete
        builder.Property(o => o.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(o => o.OrganisationId)
            .HasConstraintName("FK_Owners_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(o => o.Name).IsRequired().HasMaxLength(500);
        builder.Property(o => o.Description).HasMaxLength(2000);
        builder.Property(o => o.Url).IsRequired().HasMaxLength(2000);
        builder.Property(o => o.Platform).IsRequired().HasConversion<string>();

        // Audit timestamps
        builder.Property(o => o.DiscoveredAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(o => o.OrganisationId)
            .HasDatabaseName("IX_Owners_OrganisationId");

        builder.HasIndex(o => new { o.OrganisationId, o.Name, o.Platform })
            .HasDatabaseName("IX_Owners_OrganisationId_Name_Platform");
    }
}
