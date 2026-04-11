using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery.Services;
using Orchitect.Infrastructure.Inventory.GitHub.Services;

namespace Orchitect.Infrastructure.Inventory.GitHub.Extensions;

public static class GitHubExtensions
{
    public static IServiceCollection RegisterGitHub(this IServiceCollection services)
    {
        using var activity = Tracing.StartActivity();

        services.RegisterCache();
        services.RegisterServices();
        return services;
    }

    private static void RegisterCache(this IServiceCollection services)
    {
        services.AddMemoryCache(options => options.TrackStatistics = true);
    }

    private static void RegisterServices(this IServiceCollection services)
    {
        // Discovery service as transient (created per discovery run with credential)
        services.AddTransient<IDiscoveryService, GitHubDiscoveryService>();

        // Credential payload resolver for decrypting credentials
        services.TryAddScoped<CredentialPayloadResolver>();
    }
}