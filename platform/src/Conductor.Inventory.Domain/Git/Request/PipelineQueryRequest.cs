using Conductor.Inventory.Domain.Shared;

namespace Conductor.Inventory.Domain.Git.Request;

public sealed record PipelineQueryRequest(
    string? Id,
    string? Name,
    string? Url,
    string? OwnerName,
    PipelinePlatform? Platform) : BaseRequest;