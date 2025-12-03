using Conductor.Inventory.Domain.Discovery;
using Conductor.Inventory.Domain.Git.Service;
using Conductor.Inventory.Domain.Ticketing.Service;
using Conductor.Inventory.Infrastructure.AzureDevOps.Models;
using Conductor.Inventory.Infrastructure.AzureDevOps.Services;
using Conductor.Inventory.Infrastructure.AzureDevOps.Validation;
using Conductor.Inventory.Infrastructure.Shared.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Conductor.Inventory.Infrastructure.AzureDevOps.Extensions;

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
        services.AddScoped<IGitQueryService, AzureDevOpsGitQueryService>();
        services.AddScoped<ITicketingQueryService, AzureDevOpsTicketingQueryService>();
        services.AddScoped<IAzureDevOpsQueryService, AzureDevOpsQueryService>();
        services.AddSingleton<IDiscoveryService, AzureDevOpsDiscoveryService>();
    }

    private static void RegisterOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AzureDevOpsSettings>()
            .Bind(configuration.GetRequiredSection(nameof(AzureDevOpsSettings)));
    }
}