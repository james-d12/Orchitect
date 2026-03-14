using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Git.Request;

public sealed record PipelineQueryRequest(
    string? Id,
    string? Name,
    string? Url,
    string? OwnerName,
    PipelinePlatform? Platform) : BaseRequest;