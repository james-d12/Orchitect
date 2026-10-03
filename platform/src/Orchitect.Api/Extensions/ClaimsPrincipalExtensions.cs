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

    public static string GetRequestedBy(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.Email)
                    ?? user.FindFirst(ClaimTypes.NameIdentifier)
                    ?? throw new UnauthorizedAccessException("User claim not found");

        return claim.Value;
    }
}