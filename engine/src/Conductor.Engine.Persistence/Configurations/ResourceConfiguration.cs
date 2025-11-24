using Conductor.Engine.Domain.Environment;
using Conductor.Engine.Domain.Resource;
using Conductor.Engine.Domain.ResourceTemplate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ApplicationId = Conductor.Engine.Domain.Application.ApplicationId;

namespace Conductor.Engine.Persistence.Configurations;

internal sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");
        
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.ApplicationId, r.EnvironmentId, r.ResourceTemplateId });

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new ResourceId(value)
            ).IsRequired();

        builder.Property(r => r.ApplicationId)
            .HasConversion(
                id => id.Value,
                value => new ApplicationId(value)
            ).IsRequired();

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

        builder.Property(b => b.Name).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).IsRequired().HasDefaultValueSql("now()");
    }
}