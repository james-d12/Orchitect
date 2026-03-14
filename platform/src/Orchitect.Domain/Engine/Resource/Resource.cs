using Orchitect.Domain.Engine.Environment;
using Orchitect.Domain.Engine.ResourceTemplate;
using ApplicationId = Orchitect.Domain.Engine.Application.ApplicationId;

namespace Orchitect.Domain.Engine.Resource;

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