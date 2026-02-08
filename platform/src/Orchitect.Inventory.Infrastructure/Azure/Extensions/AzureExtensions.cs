using Orchitect.Inventory.Domain.Cloud.Service;
using Orchitect.Inventory.Domain.Discovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Inventory.Infrastructure.Azure.Models;
using Orchitect.Inventory.Infrastructure.Azure.Services;
using Orchitect.Inventory.Infrastructure.Azure.Validation;
using Orchitect.Inventory.Infrastructure.Shared.Observability;

namespace Orchitect.Inventory.Infrastructure.Azure.Extensions;

public static class AzureExtensions
{
    public static void RegisterAzure(this IServiceCollection services, IConfiguration configuration)
    {
        using var activity = Tracing.StartActivity();
        var settings = AzureSettingsValidator.GetValidSettings(configuration);

        if (!settings.IsEnabled)
        {
            return;
        }

        services.RegisterServices();
        services.RegisterCache();
        services.RegisterOptions(configuration);
    }

    private static void RegisterServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IAzureService, AzureService>();
        services.AddScoped<ICloudQueryService, AzureCloudQueryService>();
        services.AddSingleton<IDiscoveryService, AzureDiscoveryService>();
    }

    private static void RegisterCache(this IServiceCollection services)
    {
        services.AddMemoryCache(options => options.TrackStatistics = true);
    }

    private static void RegisterOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AzureSettings>()
            .Bind(configuration.GetRequiredSection(nameof(AzureSettings)));
    }
}