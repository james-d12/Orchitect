using Conductor.Inventory.Domain.Git;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Inventory.Persistence.Configurations;

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

        builder.OwnsOne(r => r.Owner, o =>
        {
            o.Property(x => x.Id)
                .HasConversion(
                    id => id.Value,
                    value => new OwnerId(value)
                );
            o.Property(x => x.Name).IsRequired();
            o.Property(x => x.Description);
            o.Property(x => x.Url).IsRequired();
            o.Property(x => x.Platform).IsRequired().HasConversion<string>();
        });
    }
}
