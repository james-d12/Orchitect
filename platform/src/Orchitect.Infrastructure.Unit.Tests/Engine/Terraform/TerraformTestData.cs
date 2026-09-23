using Orchitect.Domain.Core.Organisation;
using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine;
using Orchitect.Infrastructure.Engine.Provisioner;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

namespace Orchitect.Infrastructure.Unit.Tests.Engine.Terraform;

internal static class TerraformTestData
{
    public static ProvisionContext NewContext() =>
        new("orders", Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

    public static TerraformPlanInput PlanInput()
    {
        var template = ResourceTemplate.Create(new CreateResourceTemplateRequest
        {
            OrganisationId = new OrganisationId(),
            Name = "Storage Account",
            Type = "azure-storage-account",
            Description = "A storage account.",
            Provider = ResourceTemplateProvider.Terraform
        });

        return new TerraformPlanInput(template, new Dictionary<string, string> { ["name"] = "orders" }, "storage");
    }

    public static TerraformValidationResult.ValidResult ValidResult() =>
        TerraformValidationResult.Valid(new TerraformConfig
        {
            RequiredProviders = new Dictionary<string, TerraformConfig.RequiredProvider>
            {
                ["azurerm"] = new() { Source = "hashicorp/azurerm", VersionConstraints = ["~> 4.0"] }
            }
        }, "/modules/storage");

    public static Dictionary<TerraformPlanInput, TerraformValidationResult.ValidResult> ValidatedPlans() =>
        new() { [PlanInput()] = ValidResult() };

    public static TerraformBackendOptions AzureBackend() => new()
    {
        Mode = TerraformBackendMode.Remote,
        Type = "azurerm",
        Config = new Dictionary<string, string>
        {
            ["storage_account_name"] = "orchitectstate",
            ["container_name"] = "tfstate",
            ["key"] = "{applicationId}/{environmentId}.tfstate"
        }
    };
}
