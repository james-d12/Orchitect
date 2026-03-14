using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Engine.ResourceTemplate;

public sealed record CreateResourceTemplateRequest
{
    public required OrganisationId OrganisationId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Description { get; init; }
    public required ResourceTemplateProvider Provider { get; init; }
};