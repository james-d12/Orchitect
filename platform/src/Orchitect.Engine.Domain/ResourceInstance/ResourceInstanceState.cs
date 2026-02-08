namespace Orchitect.Engine.Domain.ResourceInstance;

public sealed record ResourceInstanceState
{
    public required Uri Location { get; init; }
    public string? Workspace { get; init; }
}