using Conductor.Inventory.Domain.Cloud.Service;
using Conductor.Inventory.Domain.Discovery;
using Conductor.Inventory.Infrastructure.Azure.Models;
using Conductor.Inventory.Infrastructure.Azure.Services;
using Conductor.Inventory.Infrastructure.Azure.Validation;
using Conductor.Inventory.Infrastructure.Shared.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Conductor.Inventory.Infrastructure.Azure.Extensions;

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