using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Cloud.Services;
using Orchitect.Domain.Inventory.Discovery.Services;
using Orchitect.Infrastructure.Inventory.Azure.Services;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.Azure.Extensions;

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