using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Issue;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("Issues", "inventory");

        builder.HasKey(wi => wi.Id);

        builder.Property(wi => wi.Id)
            .HasConversion(
                id => id.Value,
                value => new IssueId(value)
            );

        builder.Property(wi => wi.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            )
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(wi => wi.OrganisationId)
            .HasConstraintName("FK_Issues_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(wi => wi.Title).IsRequired().HasMaxLength(500);
        builder.Property(wi => wi.Description).IsRequired();
        builder.Property(wi => wi.Url).IsRequired().HasMaxLength(2000);
        builder.Property(wi => wi.Type).IsRequired().HasMaxLength(100);
        builder.Property(wi => wi.State).IsRequired().HasMaxLength(100);
        builder.Property(wi => wi.Platform).IsRequired().HasConversion<string>();

        builder.Property(wi => wi.DiscoveredAt).IsRequired();
        builder.Property(wi => wi.UpdatedAt).IsRequired();

        builder.HasIndex(wi => wi.OrganisationId);
        builder.HasIndex(wi => new { wi.OrganisationId, wi.Platform });
    }
}