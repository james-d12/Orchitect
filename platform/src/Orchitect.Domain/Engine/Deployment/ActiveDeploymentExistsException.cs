using Orchitect.Domain.Engine.Environment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Engine.Deployment;

public sealed class ActiveDeploymentExistsException(
    ApplicationId applicationId,
    EnvironmentId environmentId,
    Exception? innerException = null)
    : Exception(
        $"Application {applicationId.Value} already has a deployment running in environment {environmentId.Value}.",
        innerException)
{
    public ApplicationId ApplicationId { get; } = applicationId;
    public EnvironmentId EnvironmentId { get; } = environmentId;
}
