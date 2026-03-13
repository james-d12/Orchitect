using System.Security.Claims;
using Orchitect.Core.Domain.Organisation;
using Orchitect.Shared;

namespace Orchitect.Inventory.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static OrganisationId GetOrganisationId(this ClaimsPrincipal user)
    {
        var organisationGuid = user.GetOrganisationIdValue();
        return new OrganisationId(organisationGuid);
    }
}
