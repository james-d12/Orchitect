namespace Orchitect.Domain.Engine.Application;

public sealed record Repository
{
    public required string Name { get; init; }
    public required Uri Url { get; init; }
    public required RepositoryProvider Provider { get; init; }
}