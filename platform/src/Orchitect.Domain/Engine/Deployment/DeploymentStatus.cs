namespace Orchitect.Domain.Engine.Deployment;

public enum DeploymentStatus
{
    Pending,
    Deploying,
    Deployed,
    Failed,
    RolledBack
}