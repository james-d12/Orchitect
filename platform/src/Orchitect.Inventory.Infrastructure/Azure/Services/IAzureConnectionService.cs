using Azure.ResourceManager;

namespace Orchitect.Inventory.Infrastructure.Azure.Services;

public interface IAzureConnectionService
{
    ArmClient Client { get; }
}
