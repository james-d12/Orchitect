using Orchitect.Engine.Domain.Environment;
using Orchitect.Engine.Domain.ResourceTemplate;
using ApplicationId = Orchitect.Engine.Domain.Application.ApplicationId;

namespace Orchitect.Engine.Domain.Resource;

public sealed record Resource
{
    public required ResourceId Id { get; init; }
    public required string Name { get; init; }
    public required ResourceTemplateId ResourceTemplateId { get; init; }
    public required ApplicationId ApplicationId { get; init; }
    public required EnvironmentId EnvironmentId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}