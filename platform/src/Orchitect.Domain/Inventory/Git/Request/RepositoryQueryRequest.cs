using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Git.Request;

public sealed record RepositoryQueryRequest(
    string? Id,
    string? Name,
    string? Url,
    string? DefaultBranch,
    string? OwnerName,
    RepositoryPlatform? Platform) : BaseRequest;