using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Core.Domain.Organisation;

namespace Orchitect.Core.Persistence.Configurations;

internal sealed class OrganisationUserConfiguration : IEntityTypeConfiguration<OrganisationUser>
{
    public void Configure(EntityTypeBuilder<OrganisationUser> builder)
    {
        builder.ToTable("OrganisationUsers");

        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.IdentityUserId, s.OrganisationId })
            .IsUnique();

        builder.Property(x => x.Id)
            .HasConversion(
                id => id.Value,
                value => new OrganisationUserId(value)
            );

        builder.Property(x => x.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(x => x.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.IdentityUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}