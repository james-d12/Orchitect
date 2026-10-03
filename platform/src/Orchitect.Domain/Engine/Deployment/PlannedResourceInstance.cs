using Orchitect.Domain.Engine.ResourceInstance;

namespace Orchitect.Domain.Engine.Deployment;

public sealed record PlannedResourceInstance(ResourceInstanceId InstanceId, ResourceInstanceOutput? Output);
