using Orchitect.Engine.Domain.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Core.Domain.Organisation;
using ApplicationId = Orchitect.Engine.Domain.Application.ApplicationId;

namespace Orchitect.Engine.Persistence.Configurations;

internal sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.Name, a.OrganisationId }).IsUnique();

        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => new ApplicationId(value)
            );

        builder.Property(a => a.OrganisationId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            );

        builder.OwnsOne(a => a.Repository, r =>
        {
            r.Property(x => x.Name).IsRequired();
            r.Property(x => x.Url).IsRequired();
            r.Property(x => x.Provider).IsRequired().HasConversion<string>();
        });
    }
}