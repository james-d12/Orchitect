using Conductor.Inventory.Infrastructure.Azure.Models;
using Conductor.Inventory.Infrastructure.Shared.Validation;
using Microsoft.Extensions.Configuration;

namespace Conductor.Inventory.Infrastructure.Azure.Validation;

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