namespace Orchitect.Infrastructure.Engine.Runner;

public sealed record RunnerContext
{
    public required string Image { get; init; }

    public required string RunId { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required IReadOnlyDictionary<string, string> Configuration { get; init; }
}

public interface IRunner
{
    public Task ExecuteAsync(RunnerContext context, CancellationToken cancellationToken = default);
}
