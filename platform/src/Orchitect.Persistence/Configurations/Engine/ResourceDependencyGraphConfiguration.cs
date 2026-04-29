using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceDependency;

namespace Orchitect.Persistence.Configurations.Engine;

internal sealed class ResourceDependencyGraphConfiguration : IEntityTypeConfiguration<ResourceDependencyGraph>
{
    public void Configure(EntityTypeBuilder<ResourceDependencyGraph> builder)
    {
        builder.ToTable("ResourceDependencyGraphs");
        builder.HasKey(g => g.Id);
        builder.HasIndex(g => new { g.OrganisationId, g.EnvironmentId }).IsUnique();

        builder.Property(g => g.Id)
            .HasConversion(id => id.Value, value => new ResourceDependencyGraphId(value))
            .IsRequired();
        builder.Property(g => g.OrganisationId)
            .HasConversion(id => id.Value, value => new OrganisationId(value))
            .IsRequired();
        builder.Property(g => g.EnvironmentId)
            .HasConversion(id => id.Value, value => new EnvironmentId(value))
            .IsRequired();

        // Phase 1: store entire node/edge structure as JSONB via the private _nodes field.
        // EF Core accesses the field directly using field-only property access mode.
        builder.Property<Dictionary<ResourceId, ResourceDependencyNode>>("_nodes")
            .HasField("_nodes")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(new NodesConverter())
            .HasColumnType("jsonb")
            .HasColumnName("Nodes")
            .IsRequired();
    }

    private sealed class NodeDto
    {
        public Guid ResourceId { get; set; }
        public List<Guid> In { get; set; } = [];
        public List<Guid> Out { get; set; } = [];
    }

    private sealed class NodesConverter()
        : ValueConverter<Dictionary<ResourceId, ResourceDependencyNode>, string>(
            nodes => Serialize(nodes),
            json => Deserialize(json))
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

        private static string Serialize(Dictionary<ResourceId, ResourceDependencyNode> nodes)
        {
            var dtos = new List<NodeDto>(nodes.Count);
            foreach (var n in nodes.Values)
            {
                var dto = new NodeDto
                {
                    ResourceId = n.ResourceId.Value,
                    In = new List<Guid>(n.In.Count),
                    Out = new List<Guid>(n.Out.Count)
                };
                foreach (var id in n.In) dto.In.Add(id.Value);
                foreach (var id in n.Out) dto.Out.Add(id.Value);
                dtos.Add(dto);
            }
            return JsonSerializer.Serialize(dtos, SerializerOptions);
        }

        private static Dictionary<ResourceId, ResourceDependencyNode> Deserialize(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<NodeDto>>(json, SerializerOptions) ?? [];
            var result = new Dictionary<ResourceId, ResourceDependencyNode>(dtos.Count);
            foreach (var dto in dtos)
            {
                var node = new ResourceDependencyNode { ResourceId = new ResourceId(dto.ResourceId) };
                foreach (var id in dto.In) node.In.Add(new ResourceId(id));
                foreach (var id in dto.Out) node.Out.Add(new ResourceId(id));
                result[node.ResourceId] = node;
            }
            return result;
        }
    }
}
