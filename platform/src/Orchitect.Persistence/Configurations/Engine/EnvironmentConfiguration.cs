using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Environment = Orchitect.Domain.Engine.Environment.Environment;

namespace Orchitect.Persistence.Configurations.Engine;

internal sealed class EnvironmentConfiguration : IEntityTypeConfiguration<Environment>
{
    public void Configure(EntityTypeBuilder<Environment> builder)
    {
        builder.ToTable("Environments");

        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.Name, r.OrganisationId }).IsUnique();

        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.Description).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentId(value)
            );

        builder.Property(e => e.OrganisationId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );
    }
}