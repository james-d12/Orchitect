using Conductor.Engine.Domain.Environment;
using Application_ApplicationId = Conductor.Engine.Domain.Application.ApplicationId;

namespace Conductor.Engine.Domain.Deployment;

/// <summary>
/// Represents a deployed application to a specific environment.
/// </summary>
public sealed record Deployment
{
    public required DeploymentId Id { get; init; }
    public required Application_ApplicationId ApplicationId { get; init; }
    public required EnvironmentId EnvironmentId { get; init; }
    public required CommitId CommitId { get; init; }
    public required DeploymentStatus Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private Deployment()
    {
    }

    public static Deployment Create(Application_ApplicationId applicationId, EnvironmentId environmentId, CommitId commitId)
    {
        return new Deployment
        {
            Id = new DeploymentId(),
            ApplicationId = applicationId,
            EnvironmentId = environmentId,
            CommitId = commitId,
            Status = DeploymentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static Deployment Create(CreateDeploymentRequest request)
    {
        return Create(request.ApplicationId, request.EnvironmentId, request.CommitId);
    }
}