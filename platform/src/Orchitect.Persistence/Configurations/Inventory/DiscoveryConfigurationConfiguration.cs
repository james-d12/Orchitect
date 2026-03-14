using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Discovery;

namespace Orchitect.Persistence.Configurations.Inventory;

public class DiscoveryConfigurationConfiguration : IEntityTypeConfiguration<DiscoveryConfiguration>
{
    public void Configure(EntityTypeBuilder<DiscoveryConfiguration> builder)
    {
        builder.ToTable("DiscoveryConfigurations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(
                id => id.Value,
                value => new DiscoveryConfigurationId(value));

        builder.Property(x => x.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value))
            .IsRequired();

        builder.Property(x => x.CredentialId)
            .HasConversion(
                id => id.Value,
                value => new CredentialId(value))
            .IsRequired();

        builder.Property(x => x.Platform)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.Property(x => x.Schedule)
            .HasMaxLength(100);

        builder.Property(x => x.PlatformConfig)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.OrganisationId);
        builder.HasIndex(x => x.CredentialId);
        builder.HasIndex(x => new { x.OrganisationId, x.Platform });
        builder.HasIndex(x => x.IsEnabled);
    }
}