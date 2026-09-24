using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery.Services;
using Orchitect.Infrastructure.Inventory.GitHub.Services;

namespace Orchitect.Infrastructure.Inventory.GitHub.Extensions;

public static class GitHubExtensions
{
    internal static IServiceCollection RegisterGitHub(this IServiceCollection services)
    {
        using var activity = Tracing.StartActivity();
        services.AddTransient<IDiscoveryService, GitHubDiscoveryService>();
        services.TryAddScoped<CredentialPayloadResolver>();
        return services;
    }
}