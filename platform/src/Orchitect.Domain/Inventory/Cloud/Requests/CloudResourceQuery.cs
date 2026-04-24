using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Cloud.Requests;

public sealed record CloudResourceQuery(
    OrganisationId OrganisationId,
    string? Id = null,
    string? Name = null,
    string? Description = null,
    string? Url = null,
    string? Type = null,
    CloudPlatform? Platform = null) : BaseQuery(OrganisationId);