using System.Security.Claims;
using Orchitect.Api.Shared;
using Orchitect.Domain.Core.Organisation;

namespace Orchitect.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static OrganisationId GetOrganisationId(this ClaimsPrincipal user)
    {
        var organisationGuid = user.GetOrganisationIdValue();
        return new OrganisationId(organisationGuid);
    }
}