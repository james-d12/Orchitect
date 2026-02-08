using Orchitect.Inventory.Domain.Shared;

namespace Orchitect.Inventory.Domain.Cloud.Request;

public sealed record CloudSecretQueryRequest(
    string? Name,
    string? Location,
    string? Url,
    CloudSecretPlatform? Platform) : BaseRequest;