using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.Git;
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
    public required string RequestedBy { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorSummary { get; init; }

    public const int RequestedByMaxLength = 256;
    public const int ErrorSummaryMaxLength = 2000;

    private Deployment()
    {
    }

    public static Deployment Create(ApplicationId applicationId, EnvironmentId environmentId, CommitId commitId,
        string requestedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(requestedBy.Length, RequestedByMaxLength, nameof(requestedBy));

        if (!GitValidator.IsValidCommitId(commitId.Value))
        {
            throw new ArgumentException($"Commit id '{commitId.Value}' is not a full git commit SHA.",
                nameof(commitId));
        }

        return new Deployment
        {
            Id = new DeploymentId(),
            ApplicationId = applicationId,
            EnvironmentId = environmentId,
            CommitId = commitId,
            Status = DeploymentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RequestedBy = requestedBy
        };
    }

    public static Deployment Create(CreateDeploymentRequest request, string requestedBy)
    {
        return Create(request.ApplicationId, request.EnvironmentId, request.CommitId, requestedBy);
    }

    public Deployment Start()
    {
        if (Status != DeploymentStatus.Pending)
        {
            throw new InvalidOperationException($"Deployment '{Id.Value}' cannot start while {Status}.");
        }

        return StartRun(DeploymentStatus.Deploying);
    }

    public bool IsActive => Status is DeploymentStatus.Pending or DeploymentStatus.Deploying
        or DeploymentStatus.Destroying;

    public bool CanDestroy => Status is DeploymentStatus.Deployed or DeploymentStatus.Failed
        or DeploymentStatus.Cancelled;

    public Deployment StartDestroy()
    {
        if (!CanDestroy)
        {
            throw new InvalidOperationException($"Deployment '{Id.Value}' cannot be destroyed while {Status}.");
        }

        return StartRun(DeploymentStatus.Destroying);
    }

    public Deployment Interrupt(string reason)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Deployment '{Id.Value}' cannot be interrupted while {Status}.");
        }

        return Complete(DeploymentStatus.Failed, reason);
    }

    public Deployment Cancel()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Deployment '{Id.Value}' cannot be cancelled while {Status}.");
        }

        return Complete(DeploymentStatus.Cancelled, null);
    }

    public Deployment ProcessDeploymentStatus(long? exitCode, Exception? exception)
    {
        if (Status is not (DeploymentStatus.Deploying or DeploymentStatus.Destroying))
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
            not null => Complete(DeploymentStatus.Failed, exception.Message),
            null when exitCode == 0 => Complete(Status == DeploymentStatus.Destroying
                ? DeploymentStatus.Destroyed
                : DeploymentStatus.Deployed, null),
            _ => Complete(DeploymentStatus.Failed, $"The runner exited with code {exitCode}.")
        };
    }

    private Deployment StartRun(DeploymentStatus status)
    {
        var now = DateTime.UtcNow;

        return this with
        {
            Status = status,
            UpdatedAt = now,
            StartedAt = now,
            CompletedAt = null,
            ErrorSummary = null
        };
    }

    private Deployment Complete(DeploymentStatus status, string? errorSummary)
    {
        var now = DateTime.UtcNow;

        return this with
        {
            Status = status,
            UpdatedAt = now,
            CompletedAt = now,
            ErrorSummary = errorSummary is { Length: > ErrorSummaryMaxLength }
                ? errorSummary[..ErrorSummaryMaxLength]
                : errorSummary
        };
    }
}
