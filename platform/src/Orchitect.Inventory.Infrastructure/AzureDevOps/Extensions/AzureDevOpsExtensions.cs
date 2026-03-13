using Orchitect.Inventory.Domain.Discovery;
using Orchitect.Inventory.Domain.Git.Service;
using Orchitect.Inventory.Domain.Ticketing.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Services;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Core.Domain.Credential;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Extensions;

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