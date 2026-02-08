using Orchitect.Engine.Domain.Environment;
using ApplicationId = Orchitect.Engine.Domain.Application.ApplicationId;

namespace Orchitect.Engine.Domain.Deployment;

public sealed record CreateDeploymentRequest(
    ApplicationId ApplicationId,
    EnvironmentId EnvironmentId,
    CommitId CommitId);