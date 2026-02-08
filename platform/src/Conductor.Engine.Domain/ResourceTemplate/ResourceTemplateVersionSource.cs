namespace Conductor.Engine.Domain.ResourceTemplate;

public sealed record ResourceTemplateVersionSource
{
    public required Uri BaseUrl { get; init; }
    public required string FolderPath { get; init; }
    public required string Tag { get; init; }
}