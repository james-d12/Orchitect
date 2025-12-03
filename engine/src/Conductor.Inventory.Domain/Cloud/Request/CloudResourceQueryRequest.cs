using Conductor.Inventory.Domain.Shared;

namespace Conductor.Inventory.Domain.Cloud.Request;

public sealed record CloudResourceQueryRequest(
    string? Id,
    string? Name,
    string? Description,
    string? Url,
    string? Type,
    CloudPlatform? Platform) : BaseRequest;