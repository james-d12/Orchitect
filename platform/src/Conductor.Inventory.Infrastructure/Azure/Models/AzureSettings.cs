using Conductor.Inventory.Infrastructure.Shared;

namespace Conductor.Inventory.Infrastructure.Azure.Models;

public sealed class AzureSettings : Settings
{
    public List<string> SubscriptionFilters { get; set; } = [];
}