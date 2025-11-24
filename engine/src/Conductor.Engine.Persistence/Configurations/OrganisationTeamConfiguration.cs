using Conductor.Engine.Domain.Organisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Engine.Persistence.Configurations;

internal sealed class OrganisationTeamConfiguration : IEntityTypeConfiguration<OrganisationTeam>
{
    public void Configure(EntityTypeBuilder<OrganisationTeam> builder)
    {
        builder.ToTable("OrganisationTeams");
        
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
                value => new OrganisationTeamId(value)
            );

        builder.Property(s => s.OrganisationId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );
    }
}