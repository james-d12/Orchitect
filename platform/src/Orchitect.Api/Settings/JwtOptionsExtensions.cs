using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Orchitect.Api.Settings;

public static class JwtOptionsExtensions
{
    /// <summary>Binds <see cref="JwtOptions"/> and fails startup when the signing secret is missing or too short.</summary>
    public static IServiceCollection AddJwtOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => JwtOptions.IsSecretLongEnough(options.Secret),
                $"{JwtOptions.SectionName}:Secret must be at least {JwtOptions.MinimumSecretBytes} bytes.")
            .ValidateOnStart();

        return services;
    }
}
