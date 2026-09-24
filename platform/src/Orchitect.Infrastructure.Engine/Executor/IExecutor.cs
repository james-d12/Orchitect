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
}

public interface IExecutor
{
    public Task ExecuteAsync(ExecutorContext context, CancellationToken cancellationToken = default);
}
