using Orchitect.Domain.Engine.Environment;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Engine.Deployment;

/// <summary>
/// Represents a deployed application to a specific environment.
/// </summary>
public sealed record Deployment
{
    public required DeploymentId Id { get; init; }
    public required ApplicationId ApplicationId { get; init; }
    public required EnvironmentId EnvironmentId { get; init; }
    public required CommitId CommitId { get; init; }
    public required DeploymentStatus Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }

    private Deployment()
    {
    }

    public static Deployment Create(ApplicationId applicationId, EnvironmentId environmentId, CommitId commitId)
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

    public Deployment Start()
    {
        if (Status != DeploymentStatus.Pending)
        {
            throw new InvalidOperationException($"Deployment '{Id.Value}' cannot start while {Status}.");
        }

        return WithStatus(DeploymentStatus.Deploying);
    }

    public Deployment ProcessDeploymentStatus(long? exitCode, Exception? exception)
    {
        if (Status != DeploymentStatus.Deploying)
        {
            throw new InvalidOperationException(
                $"Deployment '{Id.Value}' cannot process a run result while {Status}.");
        }

        if (exitCode is null && exception is null)
        {
            throw new ArgumentException("A run result needs an exit code or an exception.");
        }

        return exception switch
        {
            OperationCanceledException => this,
            not null => WithStatus(DeploymentStatus.Failed),
            null when exitCode == 0 => WithStatus(DeploymentStatus.Deployed),
            null => WithStatus(DeploymentStatus.Failed)
        };
    }

    private Deployment WithStatus(DeploymentStatus status)
    {
        return this with
        {
            Status = status,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
