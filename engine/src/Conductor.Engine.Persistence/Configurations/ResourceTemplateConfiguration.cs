using Conductor.Engine.Domain.Organisation;
using Conductor.Engine.Domain.ResourceTemplate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Conductor.Engine.Persistence.Configurations;

internal sealed class ResourceTemplateConfiguration : IEntityTypeConfiguration<ResourceTemplate>
{
    public void Configure(EntityTypeBuilder<ResourceTemplate> builder)
    {
        builder.ToTable("ResourceTemplates");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.Name, r.OrganisationId }).IsUnique();

        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.Type).IsRequired();
        builder.Property(b => b.Description).IsRequired();
        builder.Property(b => b.Provider).IsRequired().HasConversion<string>();
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new ResourceTemplateId(value)
            );

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(r => r.OrganisationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(r => r.Versions, v =>
        {
            v.WithOwner().HasForeignKey("TemplateId");
            v.HasKey(rtv => rtv.Id);
            v.HasIndex(x => new { x.TemplateId, x.Version }).IsUnique();
            v.Property(x => x.Version).IsRequired();
            v.Property(x => x.Notes).IsRequired();
            v.Property(x => x.State).IsRequired().HasConversion<int>();
            v.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");

            v.Property(r => r.Id)
                .HasConversion(
                    id => id.Value,
                    value => new ResourceTemplateVersionId(value)
                );

            v.OwnsOne(r => r.Source, s =>
            {
                s.Property(p => p.BaseUrl)
                    .IsRequired()
                    .HasConversion(
                        uri => uri.ToString(),
                        str => new Uri(str)
                    );

                s.Property(p => p.FolderPath).IsRequired();
            });
        });
    }
}