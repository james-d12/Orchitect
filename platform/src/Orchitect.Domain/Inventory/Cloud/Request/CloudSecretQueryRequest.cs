using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Cloud.Request;

public sealed record CloudSecretQueryRequest(
    string? Name,
    string? Location,
    string? Url,
    CloudSecretPlatform? Platform) : BaseRequest;