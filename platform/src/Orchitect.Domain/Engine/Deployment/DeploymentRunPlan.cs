namespace Orchitect.Domain.Engine.Deployment;

public sealed record DeploymentRunPlan
{
    public required DeploymentRunId RunId { get; init; }
    public required string Contents { get; init; }
    public required IReadOnlyList<PlannedResourceInstance> Instances { get; init; }
    public required DateTime CreatedAt { get; init; }

    private DeploymentRunPlan()
    {
    }

    public static DeploymentRunPlan Create(DeploymentRunId runId, string contents,
        IReadOnlyList<PlannedResourceInstance> instances)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contents);

        return new DeploymentRunPlan
        {
            RunId = runId,
            Contents = contents,
            Instances = instances,
            CreatedAt = DateTime.UtcNow
        };
    }
}
