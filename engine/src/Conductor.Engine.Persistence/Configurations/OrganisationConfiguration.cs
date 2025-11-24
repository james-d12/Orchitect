using Conductor.Engine.Domain.Application;
using Conductor.Engine.Domain.Organisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Environment = Conductor.Engine.Domain.Environment.Environment;

namespace Conductor.Engine.Persistence.Configurations;

internal sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("Organisations");
        
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.Name).IsUnique();

        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );

        builder.HasMany<Application>()
            .WithOne()
            .HasForeignKey(a => a.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<Environment>()
            .WithOne()
            .HasForeignKey(e => e.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<OrganisationTeam>()
            .WithOne()
            .HasForeignKey(ot => ot.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<OrganisationService>()
            .WithOne()
            .HasForeignKey(o => o.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<OrganisationUser>()
            .WithOne()
            .HasForeignKey(u => u.OrganisationId)
            .IsRequired();
    }
}