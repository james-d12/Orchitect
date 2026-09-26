namespace Orchitect.Infrastructure.Engine.Executor;

public sealed record ExecutorContext
{
    public required string Image { get; init; }

    public required string RunId { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required IReadOnlyDictionary<string, string> Configuration { get; init; }

    public string? Network { get; init; }

    public string? DatabaseHost { get; init; }

    public int? DatabasePort { get; init; }

    public long? MemoryBytes { get; init; }

    public long? NanoCpus { get; init; }

    public long? PidsLimit { get; init; }

    public TimeSpan? StopGracePeriod { get; init; }

    public TimeSpan? Timeout { get; init; }
}

public sealed record ExecutorResult(long? ExitCode, Exception? Exception = null);

public interface IExecutor
{
    /// <summary>
    /// Runs the runner to completion and returns its exit code, or the exception when it could not be run.
    /// </summary>
    public Task<ExecutorResult> ExecuteAsync(ExecutorContext context, CancellationToken cancellationToken = default);
}
