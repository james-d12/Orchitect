namespace Conductor.Engine.Domain.Deployment;

public enum DeploymentStatus
{
    Pending,
    Deployed,
    Failed,
    RolledBack
}