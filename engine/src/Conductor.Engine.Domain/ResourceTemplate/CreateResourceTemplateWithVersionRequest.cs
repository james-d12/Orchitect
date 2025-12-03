using Conductor.Engine.Domain.Organisation;

namespace Conductor.Engine.Domain.ResourceTemplate;

public sealed record CreateResourceTemplateWithVersionRequest
{
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Description { get; init; }
    public required ResourceTemplateProvider Provider { get; init; }
    public required string Version { get; init; }
    public required ResourceTemplateVersionSource Source { get; init; }
    public required string Notes { get; init; }
    public required ResourceTemplateVersionState State { get; init; }
}