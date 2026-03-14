using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orchitect.Domain.Core.Credential;
using Orchitect.Infrastructure.Core.Encryption;
using Orchitect.Infrastructure.Engine;
using Orchitect.Infrastructure.Inventory;

namespace Orchitect.Infrastructure;

public static class OrchitectInfrastructureExtensions
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IEncryptionService, AesEncryptionService>();
        services.AddEngineInfrastructureServices();
        services.AddInventoryInfrastructureServices();
    }
}