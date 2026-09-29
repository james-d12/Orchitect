using Orchitect.Domain.Engine.Deployment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Infrastructure.Engine.Queue;

public enum DeploymentOperation
{
    Provision,
    Destroy
}

public sealed record DeploymentQueueRequest(
    ApplicationId ApplicationId,
    DeploymentId DeploymentId,
    DeploymentOperation Operation = DeploymentOperation.Provision);
