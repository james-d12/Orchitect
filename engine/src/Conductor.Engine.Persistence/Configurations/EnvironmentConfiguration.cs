using Conductor.Engine.Domain.Environment;
using Conductor.Engine.Domain.Organisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Environment = Conductor.Engine.Domain.Environment.Environment;

namespace Conductor.Engine.Persistence.Configurations;

internal sealed class EnvironmentConfiguration : IEntityTypeConfiguration<Environment>
{
    public void Configure(EntityTypeBuilder<Environment> builder)
    {
        builder.ToTable("Environments");

        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.Name).IsUnique();

        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.Description).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentId(value)
            );

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(a => a.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}