using Orchitect.Inventory.Domain.Cloud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orchitect.Inventory.Persistence.Configurations;

internal sealed class CloudResourceConfiguration : IEntityTypeConfiguration<CloudResource>
{
    public void Configure(EntityTypeBuilder<CloudResource> builder)
    {
        builder.ToTable("CloudResources");

        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.Id)
            .HasConversion(
                id => id.Value,
                value => new CloudResourceId(value)
            );

        builder.Property(cr => cr.Name).IsRequired();
        builder.Property(cr => cr.Description).IsRequired();
        builder.Property(cr => cr.Url).IsRequired();
        builder.Property(cr => cr.Type).IsRequired();
        builder.Property(cr => cr.Platform).IsRequired().HasConversion<string>();
    }
}
