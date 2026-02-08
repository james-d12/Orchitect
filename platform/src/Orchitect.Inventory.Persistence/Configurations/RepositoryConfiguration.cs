using Orchitect.Inventory.Domain.Git;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orchitect.Inventory.Persistence.Configurations;

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
