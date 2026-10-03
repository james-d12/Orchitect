namespace Orchitect.Domain.Engine.Deployment;

public interface IDeploymentRunPlanRepository
{
    /// <summary>
    /// Gets the plan recorded for a run, or null when the run has not been planned.
    /// </summary>
    Task<DeploymentRunPlan?> GetByRunIdAsync(DeploymentRunId runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a run's plan. A run has at most one plan, so recording a second one fails.
    /// </summary>
    Task<DeploymentRunPlan?> CreateAsync(DeploymentRunPlan plan, CancellationToken cancellationToken = default);
}
