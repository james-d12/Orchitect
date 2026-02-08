using Conductor.Engine.Domain.Environment;
using ApplicationId = Conductor.Engine.Domain.Application.ApplicationId;

namespace Conductor.Engine.Domain.Deployment;

public sealed record CreateDeploymentRequest(
    ApplicationId ApplicationId,
    EnvironmentId EnvironmentId,
    CommitId CommitId);