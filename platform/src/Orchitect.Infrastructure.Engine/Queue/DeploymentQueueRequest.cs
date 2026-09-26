using Orchitect.Domain.Engine.Deployment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Infrastructure.Engine.Queue;

public sealed record DeploymentQueueRequest(ApplicationId ApplicationId, DeploymentId DeploymentId);