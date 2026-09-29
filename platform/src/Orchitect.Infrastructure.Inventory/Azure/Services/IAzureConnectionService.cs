using Azure.ResourceManager;

namespace Orchitect.Infrastructure.Inventory.Azure.Services;

public interface IAzureConnectionService
{
    ArmClient Client { get; }
}
