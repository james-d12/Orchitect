using Conductor.Engine.Domain.Environment;
using Conductor.Engine.Domain.ResourceTemplate;
using Application_ApplicationId = Conductor.Engine.Domain.Application.ApplicationId;

namespace Conductor.Engine.Domain.Resource;

public sealed record Resource
{
    public required ResourceId Id { get; init; }
    public required string Name { get; init; }
    public required ResourceTemplateId ResourceTemplateId { get; init; }
    public required Application_ApplicationId ApplicationId { get; init; }
    public required EnvironmentId EnvironmentId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}