namespace Orchitect.Domain.Engine.Deployment;

/// <summary>
/// Represents one provision or destroy run of a deployment in a runner.
/// </summary>
public sealed record DeploymentRun
{
    public required DeploymentRunId Id { get; init; }
    public required DeploymentId DeploymentId { get; init; }
    public required DeploymentRunOperation Operation { get; init; }
    public required DeploymentRunStatus Status { get; init; }
    public required DateTime QueuedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public long? ExitCode { get; init; }
    public string? ErrorSummary { get; init; }
    public string? RunnerId { get; init; }
    public string? LogLocation { get; init; }
    public string? TokenHash { get; init; }
    public DateTime? TokenExpiresAt { get; init; }
    public DateTime? CancelRequestedAt { get; init; }
    public uint Version { get; init; }

    public const int ErrorSummaryMaxLength = 2000;
    public const int RunnerIdMaxLength = 256;
    public const int LogLocationMaxLength = 2048;
    public const int TokenHashMaxLength = 256;

    private DeploymentRun()
    {
    }

    public static DeploymentRun Queue(DeploymentId deploymentId, DeploymentRunOperation operation)
    {
        return new DeploymentRun
        {
            Id = new DeploymentRunId(),
            DeploymentId = deploymentId,
            Operation = operation,
            Status = DeploymentRunStatus.Queued,
            QueuedAt = DateTime.UtcNow
        };
    }

    public bool IsActive => Status is DeploymentRunStatus.Queued or DeploymentRunStatus.Running;

    public DeploymentRun Start()
    {
        if (Status != DeploymentRunStatus.Queued)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot start while {Status}.");
        }

        return this with
        {
            Status = DeploymentRunStatus.Running,
            StartedAt = DateTime.UtcNow
        };
    }

    public bool HasValidToken(DateTime now) => IsActive && TokenHash is not null && TokenExpiresAt > now;

    public DeploymentRun IssueToken(string tokenHash, DateTime expiresAt)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot be issued a token while {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return this with
        {
            TokenHash = tokenHash,
            TokenExpiresAt = expiresAt
        };
    }

    public DeploymentRun RequestCancel(DateTime now)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot be asked to cancel while {Status}.");
        }

        return CancelRequestedAt is null ? this with { CancelRequestedAt = now } : this;
    }

    public DeploymentRun Cancel(long? exitCode = null, string? runnerId = null)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot be cancelled while {Status}.");
        }

        return Finish(DeploymentRunStatus.Cancelled, exitCode, null, runnerId);
    }

    public DeploymentRun Succeed(long? exitCode = null, string? runnerId = null)
    {
        if (Status != DeploymentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot succeed while {Status}.");
        }

        return Finish(DeploymentRunStatus.Succeeded, exitCode, null, runnerId);
    }

    public DeploymentRun Fail(string errorSummary, long? exitCode = null, string? runnerId = null)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot fail while {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorSummary);

        return Finish(DeploymentRunStatus.Failed, exitCode, errorSummary, runnerId);
    }

    public DeploymentRun RecordExit(long exitCode, string? runnerId = null)
    {
        if (IsActive)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot record its exit while {Status}.");
        }

        return this with
        {
            ExitCode = ExitCode ?? exitCode,
            RunnerId = RunnerId ?? runnerId
        };
    }

    private DeploymentRun Finish(DeploymentRunStatus status, long? exitCode, string? errorSummary,
        string? runnerId)
    {
        return this with
        {
            Status = status,
            FinishedAt = DateTime.UtcNow,
            ExitCode = exitCode,
            RunnerId = runnerId ?? RunnerId,
            TokenHash = null,
            TokenExpiresAt = null,
            ErrorSummary = errorSummary is { Length: > ErrorSummaryMaxLength }
                ? errorSummary[..ErrorSummaryMaxLength]
                : errorSummary
        };
    }
}
