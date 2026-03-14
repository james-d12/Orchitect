using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Git.Request;

public sealed record PullRequestQueryRequest(
    string? Id,
    string? Name,
    string? Description,
    string? Url,
    List<string>? Labels,
    PullRequestPlatform? Platform) : BaseRequest;