using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery;
using Orchitect.Domain.Inventory.Git.Service;
using Orchitect.Domain.Inventory.Ticketing.Service;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Services;
using Orchitect.Infrastructure.Inventory.Shared.Observability;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Extensions;

public static class AzureDevOpsExtensions
{
    public static IServiceCollection RegisterAzureDevOps(this IServiceCollection services)
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
        services.TryAddScoped<IGitQueryService, AzureDevOpsGitQueryService>();
        services.TryAddScoped<ITicketingQueryService, AzureDevOpsTicketingQueryService>();
        services.TryAddScoped<IAzureDevOpsQueryService, AzureDevOpsQueryService>();

        // Discovery service as transient (created per discovery run with credential)
        services.AddTransient<IDiscoveryService, AzureDevOpsDiscoveryService>();

        // Credential payload resolver for decrypting credentials
        services.TryAddScoped<CredentialPayloadResolver>();
    }
}