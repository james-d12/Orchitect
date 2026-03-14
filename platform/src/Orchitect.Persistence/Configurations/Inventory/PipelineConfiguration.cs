using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Git;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> builder)
    {
        builder.ToTable("Pipelines", "inventory");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new PipelineId(value)
            );

        // Organisation FK with cascade delete
        builder.Property(p => p.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(p => p.OrganisationId)
            .HasConstraintName("FK_Pipelines_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Url).IsRequired().HasMaxLength(2000);
        builder.Property(p => p.Platform).IsRequired().HasConversion<string>();

        // Audit timestamps
        builder.Property(p => p.DiscoveredAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasOne(p => p.Owner).WithMany().IsRequired();

        // Indexes
        builder.HasIndex(p => p.OrganisationId)
            .HasDatabaseName("IX_Pipelines_OrganisationId");

        builder.HasIndex(p => new { p.OrganisationId, p.Platform })
            .HasDatabaseName("IX_Pipelines_OrganisationId_Platform");
    }
}
