using Conductor.Engine.Domain.Environment;
using Conductor.Engine.Domain.ResourceTemplate;

namespace Conductor.Engine.Domain.ResourceInstance;

public sealed record ResourceInstance
{
    public required ResourceInstanceId Id { get; init; }
    public required string Name { get; init; }
    public required ResourceTemplateVersionId TemplateVersionId { get; init; }

    public string? ExistingResourceId { get; init; }

    public required ResourceInstanceState State { get; init; }

    private readonly List<ApplicationId> _consumers = [];
    public IReadOnlyList<ApplicationId> Consumers => _consumers.AsReadOnly();

    public required EnvironmentId EnvironmentId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    public void AddConsumer(ApplicationId appId)
    {
        if (!_consumers.Contains(appId))
        {
            _consumers.Add(appId);
        }
    }
}