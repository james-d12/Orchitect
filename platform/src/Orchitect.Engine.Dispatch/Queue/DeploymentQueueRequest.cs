using Orchitect.Domain.Engine.Deployment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Engine.Dispatch.Queue;

public sealed record DeploymentQueueRequest(
    DeploymentRunId RunId,
    ApplicationId ApplicationId,
    DeploymentId DeploymentId);
