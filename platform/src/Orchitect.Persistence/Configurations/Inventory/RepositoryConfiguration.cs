using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Inventory.Git;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("Repositories");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new RepositoryId(value)
            );

        builder.Property(r => r.Name).IsRequired();
        builder.Property(r => r.Url).IsRequired();
        builder.Property(r => r.DefaultBranch).IsRequired();
        builder.Property(r => r.Platform).IsRequired().HasConversion<string>();

        builder.HasOne(r => r.Owner).WithMany().IsRequired();
    }
}
