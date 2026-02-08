using Conductor.Inventory.Domain.Shared;

namespace Conductor.Inventory.Domain.Cloud.Request;

public sealed record CloudSecretQueryRequest(
    string? Name,
    string? Location,
    string? Url,
    CloudSecretPlatform? Platform) : BaseRequest;