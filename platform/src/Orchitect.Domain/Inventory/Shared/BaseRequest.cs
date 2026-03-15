using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Shared;

public abstract record BaseRequest(OrganisationId OrganisationId, int Page = 0, int PageSize = 0);