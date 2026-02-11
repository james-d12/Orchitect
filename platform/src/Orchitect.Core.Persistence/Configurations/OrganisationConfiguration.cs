using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Core.Persistence.Configurations;

internal sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("Organisations");

        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.Name).IsUnique();

        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );

        builder.HasMany(o => o.Teams)
            .WithOne()
            .HasForeignKey(ot => ot.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);


        builder.HasMany(o => o.Users)
            .WithOne()
            .HasForeignKey(u => u.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}