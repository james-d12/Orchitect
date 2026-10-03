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

    public DeploymentRun Interrupt(string reason)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Run '{Id.Value}' cannot be interrupted while {Status}.");
        }

        return Finish(DeploymentRunStatus.Failed, null, reason);
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
            ErrorSummary = errorSummary is { Length: > ErrorSummaryMaxLength }
                ? errorSummary[..ErrorSummaryMaxLength]
                : errorSummary
        };
    }
}
