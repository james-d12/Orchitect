namespace Orchitect.Infrastructure.Engine.Executor;

public sealed record ExecutorContext
{
    public required string Image { get; init; }

    public required string RunId { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required IReadOnlyDictionary<string, string> Configuration { get; init; }
}

public interface IExecutor
{
    public Task ExecuteAsync(ExecutorContext context, CancellationToken cancellationToken = default);
}
