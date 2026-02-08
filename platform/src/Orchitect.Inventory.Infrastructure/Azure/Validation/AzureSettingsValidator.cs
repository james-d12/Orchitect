using Microsoft.Extensions.Configuration;
using Orchitect.Inventory.Infrastructure.Azure.Models;
using Orchitect.Inventory.Infrastructure.Shared.Validation;

namespace Orchitect.Inventory.Infrastructure.Azure.Validation;

public static class AzureSettingsValidator
{
    public static AzureSettings GetValidSettings(IConfiguration configuration)
    {
        return new ValidationBuilder<AzureSettings>(configuration)
            .SectionExists(nameof(AzureSettings))
            .CheckEnabled(x => x.IsEnabled, nameof(AzureSettings.IsEnabled))
            .CheckValue(x => x.SubscriptionFilters, nameof(AzureSettings.SubscriptionFilters))
            .Build();
    }
}