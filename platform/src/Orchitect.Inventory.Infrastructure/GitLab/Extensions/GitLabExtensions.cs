using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Inventory.Domain.Git.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Inventory.Infrastructure.GitLab.Models;
using Orchitect.Inventory.Infrastructure.GitLab.Services;
using Orchitect.Inventory.Infrastructure.GitLab.Validator;
using Orchitect.Inventory.Infrastructure.Shared.Observability;

namespace Orchitect.Inventory.Infrastructure.GitLab.Extensions;

public static class GitLabExtensions
{
    public static IServiceCollection RegisterGitLab(this IServiceCollection services,
        IConfiguration configuration)
    {
        using var activity = Tracing.StartActivity();
        var settings = GitLabSettingsValidator.GetValidSettings(configuration);

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
        services.TryAddSingleton<IGitLabConnectionService, GitLabConnectionService>();
        services.TryAddSingleton<IGitLabService, GitLabService>();
        services.AddScoped<IGitQueryService, GitLabGitQueryService>();
        services.AddSingleton<IDiscoveryService, GitLabDiscoveryService>();
    }

    private static void RegisterOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GitLabSettings>()
            .Bind(configuration.GetRequiredSection(nameof(GitLabSettings)));
    }
}