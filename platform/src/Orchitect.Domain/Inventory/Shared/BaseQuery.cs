using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Domain.Inventory.Shared;

public abstract record BaseQuery(OrganisationId OrganisationId, int Page = 0, int PageSize = 0);