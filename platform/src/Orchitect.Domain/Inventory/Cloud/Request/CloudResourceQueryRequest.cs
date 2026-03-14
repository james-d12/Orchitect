using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Cloud.Request;

public sealed record CloudResourceQueryRequest(
    string? Id,
    string? Name,
    string? Description,
    string? Url,
    string? Type,
    CloudPlatform? Platform) : BaseRequest;