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

    public DeploymentRun Interrupt(string reason)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot be interrupted while {Status}.");
        }

        return Finish(DeploymentRunStatus.Failed, null, reason);
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

        var run = runnerId is null ? this : this with { RunnerId = runnerId };

        return run.Finish(DeploymentRunStatus.Cancelled, exitCode, null);
    }

    public DeploymentRun Complete(long? exitCode, Exception? exception, string? runnerId = null)
    {
        if (Status != DeploymentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot complete while {Status}.");
        }

        if (exitCode is null && exception is null)
        {
            throw new ArgumentException("A run result needs an exit code or an exception.");
        }

        var run = runnerId is null ? this : this with { RunnerId = runnerId };

        return exception switch
        {
            OperationCanceledException => run,
            not null => run.Finish(DeploymentRunStatus.Failed, exitCode, exception.Message),
            null when exitCode == 0 => run.Finish(DeploymentRunStatus.Succeeded, exitCode, null),
            _ => run.Finish(DeploymentRunStatus.Failed, exitCode, $"The runner exited with code {exitCode}.")
        };
    }

    private DeploymentRun Finish(DeploymentRunStatus status, long? exitCode, string? errorSummary)
    {
        return this with
        {
            Status = status,
            FinishedAt = DateTime.UtcNow,
            ExitCode = exitCode,
            TokenHash = null,
            TokenExpiresAt = null,
            ErrorSummary = errorSummary is { Length: > ErrorSummaryMaxLength }
                ? errorSummary[..ErrorSummaryMaxLength]
                : errorSummary
        };
    }
}
