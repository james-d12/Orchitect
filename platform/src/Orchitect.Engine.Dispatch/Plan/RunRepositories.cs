using Orchitect.Domain.Engine.Application;
using Orchitect.Domain.Engine.Deployment;

namespace Orchitect.Engine.Dispatch.Plan;

public sealed record RunRepositories(
    IDeploymentRunRepository Runs,
    IDeploymentRunPlanRepository Plans,
    IDeploymentRepository Deployments,
    IApplicationRepository Applications);
