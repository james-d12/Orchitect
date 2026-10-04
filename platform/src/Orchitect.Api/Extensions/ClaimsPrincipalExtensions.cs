using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Orchitect.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetRequestedBy(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.Email)
                    ?? user.FindFirst(ClaimTypes.NameIdentifier)
                    ?? throw new UnauthorizedAccessException("User claim not found");

        return claim.Value;
    }

    public static string GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)
                    ?? user.FindFirst(JwtRegisteredClaimNames.Sub)
                    ?? throw new UnauthorizedAccessException("User ID claim not found");

        return claim.Value;
    }
}