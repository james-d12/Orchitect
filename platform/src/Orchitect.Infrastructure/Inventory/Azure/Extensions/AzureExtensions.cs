using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery.Services;
using Orchitect.Infrastructure.Inventory.Azure.Services;

namespace Orchitect.Infrastructure.Inventory.Azure.Extensions;

public static class AzureExtensions
{
    internal static IServiceCollection RegisterAzure(this IServiceCollection services)
    {
        using var activity = Tracing.StartActivity();
        services.AddTransient<IDiscoveryService, AzureDiscoveryService>();
        services.TryAddScoped<CredentialPayloadResolver>();
        return services;
    }
}