namespace Orchitect.Infrastructure.Engine.Runner;

public sealed record RunnerContext
{
    public required string Image { get; init; }

    // Identifies this execution (container name, logs).
    public required string RunId { get; init; }

    // Command line arguments passed to Orchitect.Runner.
    public required IReadOnlyList<string> Arguments { get; init; }

    // Environment variables passed to the runner container.
    public required IReadOnlyDictionary<string, string> Configuration { get; init; }
}

public interface IRunner
{
    public Task ExecuteAsync(RunnerContext context, CancellationToken cancellationToken = default);
}
