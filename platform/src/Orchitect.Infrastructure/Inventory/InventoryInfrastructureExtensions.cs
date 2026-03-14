using Microsoft.Extensions.DependencyInjection;
using Orchitect.Infrastructure.Inventory.Azure.Extensions;
using Orchitect.Infrastructure.Inventory.AzureDevOps.Extensions;
using Orchitect.Infrastructure.Inventory.GitHub.Extensions;
using Orchitect.Infrastructure.Inventory.GitLab.Extensions;

namespace Orchitect.Infrastructure.Inventory;

public static class InventoryInfrastructureExtensions
{
    internal static void AddInventoryInfrastructureServices(this IServiceCollection services)
    {
        services.RegisterAzure();
        services.RegisterAzureDevOps();
        services.RegisterGitHub();
        services.RegisterGitLab();
    }
}