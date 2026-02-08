using Orchitect.Inventory.Infrastructure.Shared;

namespace Orchitect.Inventory.Infrastructure.Azure.Models;

public sealed class AzureSettings : Settings
{
    public List<string> SubscriptionFilters { get; set; } = [];
}