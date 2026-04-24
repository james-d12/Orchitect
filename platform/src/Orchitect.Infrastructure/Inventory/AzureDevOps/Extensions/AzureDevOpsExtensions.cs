using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery.Services;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Services;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Extensions;

public static class AzureDevOpsExtensions
{
    internal static IServiceCollection RegisterAzureDevOps(this IServiceCollection services)
    {
        using var activity = Tracing.StartActivity();

        services.AddTransient<IDiscoveryService, AzureDevOpsDiscoveryService>();
        services.TryAddScoped<CredentialPayloadResolver>();

        return services;
    }
}