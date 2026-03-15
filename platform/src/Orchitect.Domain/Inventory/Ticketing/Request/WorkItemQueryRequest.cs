using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Inventory.Shared;

namespace Orchitect.Domain.Inventory.Ticketing.Request;

public sealed record WorkItemQueryRequest(
    OrganisationId OrganisationId,
    string? Id = null,
    string? Title = null) : BaseRequest(OrganisationId);