using Conductor.Engine.Domain.Application;
using Conductor.Engine.Domain.Organisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ApplicationId = Conductor.Engine.Domain.Application.ApplicationId;

namespace Conductor.Engine.Persistence.Configurations;

internal sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(a => a.Id);

        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => new ApplicationId(value)
            );

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(a => a.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(a => a.Repository, r =>
        {
            r.Property(x => x.Name).IsRequired();
            r.Property(x => x.Url).IsRequired();
            r.Property(x => x.Provider).IsRequired().HasConversion<string>();
        });
    }
}