namespace Orchitect.Engine.Dispatch.Executor;

public sealed record ExecutorContext
{
    public required string Image { get; init; }

    public required string RunId { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required IReadOnlyDictionary<string, string> Configuration { get; init; }

    public IReadOnlyDictionary<string, string> Secrets { get; init; } = new Dictionary<string, string>();

    public string? Network { get; init; }

    public string? DatabaseHost { get; init; }

    public int? DatabasePort { get; init; }

    public long? MemoryBytes { get; init; }

    public long? NanoCpus { get; init; }

    public long? PidsLimit { get; init; }

    public TimeSpan? StopGracePeriod { get; init; }

    public TimeSpan? Timeout { get; init; }

    public CancellationToken StopRequested { get; init; }
}

public sealed record ExecutorResult(
    long? ExitCode,
    Exception? Exception = null,
    string? RunnerId = null,
    bool Stopped = false);

public interface IExecutor
{
    /// <summary>
    /// Runs the runner to completion and returns its exit code, or the exception when it could not be run.
    /// When <see cref="ExecutorContext.StopRequested"/> fires, the runner is stopped gracefully and the result is marked stopped.
    /// </summary>
    public Task<ExecutorResult> ExecuteAsync(ExecutorContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends SIGTERM to the running runner of a run, wherever it was started from. Returns false when it is not running.
    /// </summary>
    public Task<bool> SignalStopAsync(string runId, CancellationToken cancellationToken = default);
}
