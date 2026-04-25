namespace Orchitect.Domain.Engine.ResourceInstance;

public enum ResourceInstanceStatus
{
    Pending,
    Provisioning,
    Active,
    Failed,
    PendingRemoval,
    Removing,
    Removed,
    RemovalFailed
}
