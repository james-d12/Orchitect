using Orchitect.Inventory.Domain.Cloud.Service;
using Orchitect.Inventory.Domain.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Inventory.Infrastructure.Azure.Services;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.Azure.Extensions;

public static class AzureExtensions
{
    public static void RegisterAzure(this IServiceCollection services)
    {
        using var activity = Tracing.StartActivity();

        services.RegisterServices();
        services.RegisterCache();
    }

    private static void RegisterServices(this IServiceCollection services)
    {
        services.AddScoped<ICloudQueryService, AzureCloudQueryService>();

        // Discovery service as transient (created per discovery run with credential)
        services.AddTransient<IDiscoveryService, AzureDiscoveryService>();

        // Credential payload resolver for decrypting credentials
        services.TryAddScoped<CredentialPayloadResolver>();
    }

    private static void RegisterCache(this IServiceCollection services)
    {
        services.AddMemoryCache(options => options.TrackStatistics = true);
    }
}