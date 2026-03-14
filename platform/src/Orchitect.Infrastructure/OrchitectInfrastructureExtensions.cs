using Microsoft.Extensions.DependencyInjection;
using Orchitect.Infrastructure.Engine;
using Orchitect.Infrastructure.Inventory;

namespace Orchitect.Infrastructure;

public static class OrchitectInfrastructureExtensions
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddEngineInfrastructureServices();
        services.AddInventoryInfrastructureServices();
    }
}