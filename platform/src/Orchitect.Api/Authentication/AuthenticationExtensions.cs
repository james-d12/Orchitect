using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Orchitect.Api.Settings;

namespace Orchitect.Api.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddOrchitectAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddJwtOptions(configuration);

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateActor = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateAudience = true,
                    ValidateSignatureLast = true,

                    ValidIssuer = jwtOptions.Value.Issuer,
                    ValidAudience = jwtOptions.Value.Audience,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidTypes = [AccessTokenDefaults.TokenType],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, RunnerAuthenticationHandler>(
                RunnerAuthenticationDefaults.AuthenticationScheme, null);

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(AccessTokenDefaults.ScopeClaim, AccessTokenDefaults.Scope)
                .Build())
            .AddPolicy(RunnerAuthenticationDefaults.PolicyName, policy => policy
                .AddAuthenticationSchemes(RunnerAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(RunnerAuthenticationDefaults.RunIdClaim));

        return services;
    }
}
