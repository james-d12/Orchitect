namespace Orchitect.Domain.Engine.Deployment;

public enum DeploymentRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}
