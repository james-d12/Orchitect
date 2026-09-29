using Orchitect.Domain.Engine.Deployment;
using Orchitect.Engine.Contracts.Runner;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Engine.Dispatch.Queue;

public sealed record DeploymentQueueRequest(
    ApplicationId ApplicationId,
    DeploymentId DeploymentId,
    RunnerOperation Operation = RunnerOperation.Provision);
