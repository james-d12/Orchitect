using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Inventory.Domain.Git.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Inventory.Infrastructure.GitLab.Services;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.GitLab.Extensions;

public static class GitLabExtensions
{
    public static IServiceCollection RegisterGitLab(this IServiceCollection services)
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
        services.AddScoped<IGitQueryService, GitLabGitQueryService>();

        // Discovery service as transient (created per discovery run with credential)
        services.AddTransient<IDiscoveryService, GitLabDiscoveryService>();

        // Credential payload resolver for decrypting credentials
        services.TryAddScoped<CredentialPayloadResolver>();
    }
}