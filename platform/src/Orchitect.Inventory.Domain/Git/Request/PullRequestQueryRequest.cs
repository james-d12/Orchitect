using Orchitect.Inventory.Domain.Shared;

namespace Orchitect.Inventory.Domain.Git.Request;

public sealed record PullRequestQueryRequest(
    string? Id,
    string? Name,
    string? Description,
    string? Url,
    List<string>? Labels,
    PullRequestPlatform? Platform) : BaseRequest;