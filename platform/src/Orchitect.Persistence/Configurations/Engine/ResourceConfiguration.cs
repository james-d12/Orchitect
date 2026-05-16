using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceTemplate;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Persistence.Configurations.Engine;

internal sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.EnvironmentId, r.ResourceTemplateId });

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new ResourceId(value)
            ).IsRequired();

        builder.Property(r => r.OrganisationId)
            .HasConversion(
                id => id.Value,
                value => new OrganisationId(value)
            ).IsRequired();

        builder.Property(r => r.ApplicationId)
            .HasConversion(
                id => id.HasValue ? (Guid?)id.Value.Value : null,
                value => value.HasValue ? new ApplicationId(value.Value) : (ApplicationId?)null
            );

        builder.Property(r => r.EnvironmentId)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentId(value)
            ).IsRequired();

        builder.Property(r => r.ResourceTemplateId)
            .HasConversion(
                id => id.Value,
                value => new ResourceTemplateId(value)
            ).IsRequired();

        builder.Property(r => r.Name).IsRequired();
        builder.Property(r => r.Slug).IsRequired();
        builder.Property(r => r.Description).IsRequired();
        builder.Property(r => r.Kind).IsRequired().HasConversion<string>();
        builder.Property(r => r.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(r => r.UpdatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");

        builder.Ignore(r => r.Consumers);
    }
}