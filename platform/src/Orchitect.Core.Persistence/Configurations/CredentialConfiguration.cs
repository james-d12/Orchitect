using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Core.Domain.Credential;
using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Core.Persistence.Configurations;

internal sealed class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.ToTable("Credentials");

        builder.HasKey(c => c.Id);
        builder.HasIndex(c => new { c.Name, c.OrganisationId }).IsUnique();

        builder.Property(c => c.Name).IsRequired();
        builder.Property(c => c.EncryptedPayload).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(c => c.UpdatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");

        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => new CredentialId(value)
            );

        builder.Property(c => c.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(c => c.Platform)
            .HasConversion<string>()
            .IsRequired();

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(c => c.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
