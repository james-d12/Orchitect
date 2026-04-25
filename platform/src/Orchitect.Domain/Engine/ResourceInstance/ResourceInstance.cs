using System.Text.Json;
using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Resource;
using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Domain.Engine.ResourceInstance;

public sealed record ResourceInstance
{
    public ResourceInstanceId Id { get; private init; }
    public OrganisationId OrganisationId { get; private init; }
    public ResourceId ResourceId { get; private init; }
    public string Name { get; private set; } = string.Empty;
    public ResourceTemplateVersionId TemplateVersionId { get; private init; }
    public EnvironmentId EnvironmentId { get; private init; }
    public ResourceInstanceStatus Status { get; private set; }
    public ResourceInstanceOutput? Output { get; private set; }
    public IReadOnlyDictionary<string, JsonElement> InputParameters { get; private init; } = new Dictionary<string, JsonElement>();
    public DateTime CreatedAt { get; private init; }
    public DateTime UpdatedAt { get; private set; }

    private ResourceInstance() { }

    private static readonly Dictionary<ResourceInstanceStatus, HashSet<ResourceInstanceStatus>> ValidTransitions = new()
    {
        [ResourceInstanceStatus.Pending]        = [ResourceInstanceStatus.Provisioning],
        [ResourceInstanceStatus.Provisioning]   = [ResourceInstanceStatus.Active, ResourceInstanceStatus.Failed],
        [ResourceInstanceStatus.Active]         = [ResourceInstanceStatus.Provisioning, ResourceInstanceStatus.PendingRemoval],
        [ResourceInstanceStatus.Failed]         = [ResourceInstanceStatus.Pending],
        [ResourceInstanceStatus.PendingRemoval] = [ResourceInstanceStatus.Removing],
        [ResourceInstanceStatus.Removing]       = [ResourceInstanceStatus.Removed, ResourceInstanceStatus.RemovalFailed],
        [ResourceInstanceStatus.Removed]        = [],
        [ResourceInstanceStatus.RemovalFailed]  = [ResourceInstanceStatus.PendingRemoval]
    };

    public static ResourceInstance Create(CreateResourceInstanceRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.Name);
        return new ResourceInstance
        {
            Id = new ResourceInstanceId(),
            OrganisationId = request.OrganisationId,
            ResourceId = request.ResourceId,
            Name = request.Name,
            TemplateVersionId = request.TemplateVersionId,
            EnvironmentId = request.EnvironmentId,
            Status = ResourceInstanceStatus.Pending,
            Output = null,
            InputParameters = request.InputParameters ?? new Dictionary<string, JsonElement>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Transition(ResourceInstanceStatus newStatus, ResourceInstanceOutput? output = null)
    {
        if (!ValidTransitions[Status].Contains(newStatus))
            throw new InvalidOperationException($"Cannot transition from {Status} to {newStatus}.");
        if (newStatus == ResourceInstanceStatus.Active)
            ArgumentNullException.ThrowIfNull(output);
        Status = newStatus;
        if (output is not null) Output = output;
        UpdatedAt = DateTime.UtcNow;
    }
}
