using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Discovery;

namespace Orchitect.Persistence.Configurations.Inventory;

internal sealed class DiscoveryConfigurationConfiguration : IEntityTypeConfiguration<DiscoveryConfiguration>
{
    public void Configure(EntityTypeBuilder<DiscoveryConfiguration> builder)
    {
        builder.ToTable("DiscoveryConfigurations", "inventory");

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

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(x => x.OrganisationId)
            .HasConstraintName("FK_DiscoveryConfigurations_Organisations")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.CredentialId)
            .HasConversion(
                id => id.Value,
                value => new CredentialId(value))
            .IsRequired();

        builder.HasOne<Credential>()
            .WithMany()
            .HasForeignKey(x => x.CredentialId)
            .HasConstraintName("FK_DiscoveryConfigurations_Credentials")
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.HasIndex(x => x.OrganisationId)
            .HasDatabaseName("IX_DiscoveryConfigurations_OrganisationId");
        builder.HasIndex(x => x.CredentialId)
            .HasDatabaseName("IX_DiscoveryConfigurations_CredentialId");
        builder.HasIndex(x => new { x.OrganisationId, x.Platform })
            .HasDatabaseName("IX_DiscoveryConfigurations_OrganisationId_Platform");
        builder.HasIndex(x => x.IsEnabled)
            .HasDatabaseName("IX_DiscoveryConfigurations_IsEnabled");
    }
}