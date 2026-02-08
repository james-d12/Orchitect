using Orchitect.Inventory.Domain.Git;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orchitect.Inventory.Persistence.Configurations;

internal sealed class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> builder)
    {
        builder.ToTable("Owners");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => new OwnerId(value)
            );

        builder.Property(o => o.Name).IsRequired();
        builder.Property(o => o.Description);
        builder.Property(o => o.Url).IsRequired();
        builder.Property(o => o.Platform).IsRequired().HasConversion<string>();
    }
}
