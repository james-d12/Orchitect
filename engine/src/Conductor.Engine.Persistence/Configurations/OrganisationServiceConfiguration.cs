using Conductor.Engine.Domain.Organisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Engine.Persistence.Configurations;

internal sealed class OrganisationServiceConfiguration : IEntityTypeConfiguration<OrganisationService>
{
    public void Configure(EntityTypeBuilder<OrganisationService> builder)
    {
        builder.ToTable("OrganisationServices");
        
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.Name, s.OrganisationId })
            .IsUnique();

        builder.Property(s => s.Name).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.Property(s => s.Id)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => new OrganisationServiceId(value)
            );

        builder.Property(s => s.OrganisationId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );
    }
}