using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.ResourceTemplate;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Engine.Resource;

public sealed record Resource
{
    public ResourceId Id { get; private init; }
    public OrganisationId OrganisationId { get; private init; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private init; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ResourceTemplateId ResourceTemplateId { get; private init; }
    public ApplicationId? ApplicationId { get; private init; }
    public EnvironmentId EnvironmentId { get; private init; }
    public ResourceKind Kind { get; private init; }
    public DateTime CreatedAt { get; private init; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<ApplicationId> _consumers = [];
    public IReadOnlyList<ApplicationId> Consumers => _consumers.AsReadOnly();

    private Resource() { }

    public static Resource Create(CreateResourceRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.Name);
        return new Resource
        {
            Id = new ResourceId(),
            OrganisationId = request.OrganisationId,
            Name = request.Name,
            Slug = request.Name.ToLowerInvariant().Replace(' ', '-'),
            Description = request.Description,
            ResourceTemplateId = request.ResourceTemplateId,
            ApplicationId = request.ApplicationId,
            EnvironmentId = request.EnvironmentId,
            Kind = request.Kind,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void AddConsumer(ApplicationId appId)
    {
        if (!_consumers.Contains(appId))
            _consumers.Add(appId);
    }
}