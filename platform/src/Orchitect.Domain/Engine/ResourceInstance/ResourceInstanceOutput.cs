namespace Orchitect.Domain.Engine.ResourceInstance;

public sealed record ResourceInstanceOutput
{
    public required Uri Location { get; init; }
    public string? Workspace { get; init; }
}
