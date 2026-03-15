using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Git.Request;

public sealed record PullRequestQueryRequest(
    OrganisationId OrganisationId,
    string? Id = null,
    string? Name = null,
    string? Description = null,
    string? Url = null,
    List<string>? Labels = null,
    PullRequestPlatform? Platform = null) : BaseRequest(OrganisationId);