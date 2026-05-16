using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceInstance;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Persistence.Configurations.Engine;

internal sealed class ResourceInstanceConfiguration : IEntityTypeConfiguration<ResourceInstance>
{
    public void Configure(EntityTypeBuilder<ResourceInstance> builder)
    {
        builder.ToTable("ResourceInstances");
        builder.HasKey(ri => ri.Id);
        builder.HasIndex(ri => new { ri.ResourceId, ri.EnvironmentId });

        builder.Property(ri => ri.Id)
            .HasConversion(id => id.Value, value => new ResourceInstanceId(value))
            .IsRequired();
        builder.Property(ri => ri.OrganisationId)
            .HasConversion(id => id.Value, value => new OrganisationId(value))
            .IsRequired();
        builder.Property(ri => ri.ResourceId)
            .HasConversion(id => id.Value, value => new ResourceId(value))
            .IsRequired();
        builder.Property(ri => ri.EnvironmentId)
            .HasConversion(id => id.Value, value => new EnvironmentId(value))
            .IsRequired();
        builder.Property(ri => ri.TemplateVersionId)
            .HasConversion(id => id.Value, value => new ResourceTemplateVersionId(value))
            .IsRequired();

        builder.Property(ri => ri.Name).IsRequired();
        builder.Property(ri => ri.Status).HasConversion<string>().IsRequired();
        builder.Property(ri => ri.CreatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");
        builder.Property(ri => ri.UpdatedAt).IsRequired().HasDefaultValueSql("timezone('utc', now())");

        builder.Property(ri => ri.InputParameters)
            .HasConversion(new InputParametersConverter())
            .HasColumnType("jsonb")
            .IsRequired();

        builder.OwnsOne(ri => ri.Output, output =>
        {
            output.Property(o => o.Location)
                .HasConversion(uri => uri.ToString(), value => new Uri(value))
                .HasColumnName("OutputLocation");
            output.Property(o => o.Workspace).HasColumnName("OutputWorkspace");
        });
    }

    private sealed class InputParametersConverter()
        : ValueConverter<IReadOnlyDictionary<string, JsonElement>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(v, (JsonSerializerOptions?)null)!);
}
