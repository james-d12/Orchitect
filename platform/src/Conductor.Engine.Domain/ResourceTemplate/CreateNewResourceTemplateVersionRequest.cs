namespace Conductor.Engine.Domain.ResourceTemplate;

public sealed record CreateNewResourceTemplateVersionRequest
{
    public required string Version { get; init; }
    public required ResourceTemplateVersionSource Source { get; init; }
    public required string Notes { get; init; }
    public required ResourceTemplateVersionState State { get; init; }
}