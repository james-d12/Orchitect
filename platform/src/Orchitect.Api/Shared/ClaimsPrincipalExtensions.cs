using System.Security.Claims;

namespace Orchitect.Api.Shared;

public static class ClaimsPrincipalExtensions
{
    private const string OrganisationIdClaimType = "OrganisationId";

    public static Guid GetOrganisationIdValue(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(OrganisationIdClaimType)
            ?? user.FindFirst(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Organisation ID claim not found");

        if (!Guid.TryParse(claim.Value, out var organisationGuid))
        {
            throw new UnauthorizedAccessException("Invalid organisation ID in claims");
        }

        return organisationGuid;
    }
}
