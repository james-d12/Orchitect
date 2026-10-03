using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Orchitect.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddOrchitectAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateActor = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateAudience = true,
                    ValidateSignatureLast = true,

                    ValidIssuer = configuration["JwtOptions:Issuer"],
                    ValidAudience = configuration["JwtOptions:Audience"],
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["JwtOptions:Secret"]!)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            })
            .AddScheme<AuthenticationSchemeOptions, RunnerAuthenticationHandler>(
                RunnerAuthenticationDefaults.AuthenticationScheme, null);

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(RunnerAuthenticationDefaults.PolicyName, policy => policy
                .AddAuthenticationSchemes(RunnerAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(RunnerAuthenticationDefaults.RunIdClaim));

        return services;
    }
}
