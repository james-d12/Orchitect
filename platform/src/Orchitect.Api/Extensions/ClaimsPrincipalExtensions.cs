using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
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

    public static string GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)
                    ?? user.FindFirst(JwtRegisteredClaimNames.Sub)
                    ?? throw new UnauthorizedAccessException("User ID claim not found");

        return claim.Value;
    }
}