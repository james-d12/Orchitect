namespace Orchitect.Domain.Engine.Deployment;

public readonly record struct DeploymentRunId(Guid Value)
{
    public DeploymentRunId() : this(Guid.NewGuid())
    {
    }
}
