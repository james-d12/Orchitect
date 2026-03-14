using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Cloud;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class CloudSecretConfiguration : IEntityTypeConfiguration<CloudSecret>
{
    public void Configure(EntityTypeBuilder<CloudSecret> builder)
    {
        builder.ToTable("CloudSecrets", "inventory");

        builder.HasKey(cs => new { cs.OrganisationId, cs.Name, cs.Location, cs.Platform });

        // Organisation FK with cascade delete
        builder.Property(cs => cs.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(cs => cs.OrganisationId)
            .HasConstraintName("FK_CloudSecrets_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(cs => cs.Name).IsRequired().HasMaxLength(500);
        builder.Property(cs => cs.Location).IsRequired().HasMaxLength(500);
        builder.Property(cs => cs.Url).IsRequired().HasMaxLength(2000);
        builder.Property(cs => cs.Platform).IsRequired().HasConversion<string>();

        // Audit timestamps
        builder.Property(cs => cs.DiscoveredAt).IsRequired();
        builder.Property(cs => cs.UpdatedAt).IsRequired();

        // Index on OrganisationId
        builder.HasIndex(cs => cs.OrganisationId)
            .HasDatabaseName("IX_CloudSecrets_OrganisationId");
    }
}
