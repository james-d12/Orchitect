using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Core.Domain.Organisation;
using Orchitect.Engine.Domain.Service;

namespace Orchitect.Engine.Persistence.Configurations;

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");

        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.Name, s.OrganisationId })
            .IsUnique();

        builder.Property(s => s.Name).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(s => s.UpdatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");

        builder.Property(s => s.Id)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => new ServiceId(value)
            );

        builder.Property(s => s.OrganisationId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );
    }
}