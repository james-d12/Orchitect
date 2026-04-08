using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.SourceControl.Requests;

public sealed record RepositoryQueryRequest(
    OrganisationId OrganisationId,
    string? Id = null,
    string? Name = null,
    string? Url = null,
    string? DefaultBranch = null,
    string? OwnerName = null,
    RepositoryPlatform? Platform = null) : BaseRequest(OrganisationId);