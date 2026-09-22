namespace Orchitect.Infrastructure.Engine.Runner;

public sealed record RunnerOptions
{
    public required string Image { get; init; }
    public Dictionary<string, string> Configuration { get; init; } = [];
}
