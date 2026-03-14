using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Inventory.Git;

namespace Orchitect.Persistence.Configurations.Inventory;

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
