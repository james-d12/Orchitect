using Microsoft.Extensions.Configuration;
using Orchitect.Inventory.Infrastructure.AzureDevOps.Models;
using Orchitect.Inventory.Infrastructure.Shared.Observability;
using Orchitect.Inventory.Infrastructure.Shared.Validation;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Validation;

public static class AzureDevOpsSettingsValidator
{
    public static AzureDevOpsSettings GetValidSettings(IConfiguration configuration)
    {
        using var activity = Tracing.StartActivity();
        return new ValidationBuilder<AzureDevOpsSettings>(configuration)
            .SectionExists(nameof(AzureDevOpsSettings))
            .CheckEnabled(x => x.IsEnabled, nameof(AzureDevOpsSettings.IsEnabled))
            .CheckValue(x => x.Organization, nameof(AzureDevOpsSettings.Organization))
            .CheckValue(x => x.PersonalAccessToken, nameof(AzureDevOpsSettings.PersonalAccessToken))
            .CheckValue(x => x.ProjectFilters, nameof(AzureDevOpsSettings.ProjectFilters))
            .Build();
    }
}