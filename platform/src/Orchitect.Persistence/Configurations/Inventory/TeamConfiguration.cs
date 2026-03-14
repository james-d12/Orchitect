using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams", "inventory");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value,
                value => new TeamId(value)
            );

        // Organisation FK with cascade delete
        builder.Property(t => t.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(t => t.OrganisationId)
            .HasConstraintName("FK_Teams_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Url).IsRequired().HasMaxLength(2000);
        builder.Property(t => t.Platform).IsRequired().HasConversion<string>();

        // Audit timestamps
        builder.Property(t => t.DiscoveredAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(t => t.OrganisationId)
            .HasDatabaseName("IX_Teams_OrganisationId");

        builder.HasIndex(t => new { t.OrganisationId, t.Platform })
            .HasDatabaseName("IX_Teams_OrganisationId_Platform");

        builder.HasIndex(t => t.Url)
            .IsUnique()
            .HasDatabaseName("IX_Teams_Url");
    }
}
