using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Inventory.Domain.Git.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Inventory.Infrastructure.GitHub.Models;
using Orchitect.Inventory.Infrastructure.GitHub.Services;
using Orchitect.Inventory.Infrastructure.GitHub.Validator;
using Orchitect.Inventory.Infrastructure.Shared.Observability;

namespace Orchitect.Inventory.Infrastructure.GitHub.Extensions;

public static class GitHubExtensions
{
    public static IServiceCollection RegisterGitHub(this IServiceCollection services,
        IConfiguration configuration)
    {
        using var activity = Tracing.StartActivity();
        var settings = GitHubSettingsValidator.GetValidSettings(configuration);

        if (!settings.IsEnabled)
        {
            return services;
        }

        services.RegisterCache();
        services.RegisterServices();
        services.RegisterOptions(configuration);
        return services;
    }

    private static void RegisterCache(this IServiceCollection services)
    {
        services.AddMemoryCache(options => options.TrackStatistics = true);
    }

    private static void RegisterServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IGitHubConnectionService, GitHubConnectionService>();
        services.TryAddSingleton<IGitHubService, GitHubService>();
        services.AddScoped<IGitQueryService, GitHubGitQueryService>();
        services.AddSingleton<IDiscoveryService, GitHubDiscoveryService>();
    }

    private static void RegisterOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GitHubSettings>()
            .Bind(configuration.GetRequiredSection(nameof(GitHubSettings)));
    }
}