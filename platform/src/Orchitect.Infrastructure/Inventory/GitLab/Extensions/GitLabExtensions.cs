using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Common.Observability;
using Orchitect.Domain.Core.Credential;
using Orchitect.Domain.Inventory.Discovery.Services;
using Orchitect.Infrastructure.Inventory.GitLab.Services;

namespace Orchitect.Infrastructure.Inventory.GitLab.Extensions;

public static class GitLabExtensions
{
    internal static IServiceCollection RegisterGitLab(this IServiceCollection services)
    {
        using var activity = Tracing.StartActivity();
        services.AddTransient<IDiscoveryService, GitLabDiscoveryService>();
        services.TryAddScoped<CredentialPayloadResolver>();
        return services;
    }
}