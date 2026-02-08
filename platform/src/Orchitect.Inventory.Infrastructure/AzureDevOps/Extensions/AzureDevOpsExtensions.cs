using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Inventory.Domain.Git.Service;
using Orchitect.Inventory.Domain.Ticketing.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Models;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Services;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Validation;
using Orchitect.Inventory.Infrastructure.Shared.Observability;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Extensions;

public static class AzureDevOpsExtensions
{
    public static IServiceCollection RegisterAzureDevOps(this IServiceCollection services,
        IConfiguration configuration)
    {
        using var activity = Tracing.StartActivity();
        var settings = AzureDevOpsSettingsValidator.GetValidSettings(configuration);

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
        services.TryAddSingleton<IAzureDevOpsService, AzureDevOpsService>();
        services.TryAddSingleton<IAzureDevOpsConnectionService, AzureDevOpsConnectionService>();
        services.TryAddScoped<IGitQueryService, AzureDevOpsGitQueryService>();
        services.TryAddScoped<ITicketingQueryService, AzureDevOpsTicketingQueryService>();
        services.TryAddScoped<IAzureDevOpsQueryService, AzureDevOpsQueryService>();
        services.TryAddSingleton<IDiscoveryService, AzureDevOpsDiscoveryService>();
    }

    private static void RegisterOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AzureDevOpsSettings>()
            .Bind(configuration.GetRequiredSection(nameof(AzureDevOpsSettings)));
    }
}