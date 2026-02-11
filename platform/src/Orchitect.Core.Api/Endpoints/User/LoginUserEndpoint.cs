using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Orchitect.Core.Api.Configuration;
using Orchitect.Shared;

namespace Orchitect.Core.Application.User;

public sealed class LoginUserEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder builder) => builder
        .MapPost("/login", HandleAsync)
        .WithSummary("Login for a user.");

    private sealed record LoginUserResponse(string AccessToken);

    private sealed record LoginUserRequest(string Email, string Password);

    private static async Task<Results<Ok<LoginUserResponse>, UnauthorizedHttpResult>> HandleAsync(
        [FromBody]
        LoginUserRequest request,
        [FromServices]
        UserManager<IdentityUser> userManager,
        [FromServices]
        IOptions<JwtOptions> options)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid)
        {
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var roleClaims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        var claims = new List<Claim>()
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
        };

        claims.AddRange(roleClaims);

        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(options.Value.ExpirationInMinutes),
            SigningCredentials = credentials,
            Issuer = options.Value.Issuer,
            Audience = options.Value.Audience,
            IssuedAt = DateTime.UtcNow
        };

        var tokenHandler = new JsonWebTokenHandler();

        var accessToken = tokenHandler.CreateToken(tokenDescriptor);

        return TypedResults.Ok(new LoginUserResponse(accessToken));
    }
}