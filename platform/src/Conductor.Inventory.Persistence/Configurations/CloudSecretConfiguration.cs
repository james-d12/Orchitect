using Conductor.Inventory.Domain.Cloud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Inventory.Persistence.Configurations;

internal sealed class CloudSecretConfiguration : IEntityTypeConfiguration<CloudSecret>
{
    public void Configure(EntityTypeBuilder<CloudSecret> builder)
    {
        builder.ToTable("CloudSecrets");

        builder.HasKey(cs => new { cs.Name, cs.Location, cs.Platform });

        builder.Property(cs => cs.Name).IsRequired();
        builder.Property(cs => cs.Location).IsRequired();
        builder.Property(cs => cs.Url).IsRequired();
        builder.Property(cs => cs.Platform).IsRequired().HasConversion<string>();
    }
}
