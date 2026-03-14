using Orchitect.Domain.Engine.Environment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Engine.Deployment;

public sealed record CreateDeploymentRequest(
    ApplicationId ApplicationId,
    EnvironmentId EnvironmentId,
    CommitId CommitId);