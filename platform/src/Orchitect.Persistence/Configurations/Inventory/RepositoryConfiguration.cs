using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.SourceControl;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("Repositories", "inventory");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new RepositoryId(value)
            );

        // Organisation FK with cascade delete
        builder.Property(r => r.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(r => r.OrganisationId)
            .HasConstraintName("FK_Repositories_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(500);
        builder.Property(r => r.Url).IsRequired().HasMaxLength(2000);
        builder.Property(r => r.DefaultBranch).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Platform).IsRequired().HasConversion<string>();

        // Audit timestamps
        builder.Property(r => r.DiscoveredAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasOne(r => r.User).WithMany().IsRequired();

        // Indexes
        builder.HasIndex(r => r.OrganisationId)
            .HasDatabaseName("IX_Repositories_OrganisationId");

        builder.HasIndex(r => new { r.OrganisationId, r.Platform })
            .HasDatabaseName("IX_Repositories_OrganisationId_Platform");

        builder.HasIndex(r => r.Url)
            .IsUnique()
            .HasDatabaseName("IX_Repositories_Url");
    }
}
