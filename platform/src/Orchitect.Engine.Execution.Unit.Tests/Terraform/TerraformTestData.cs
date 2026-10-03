using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Contracts.Terraform;
using Orchitect.Engine.Execution.Provisioner.Terraform.Models;

namespace Orchitect.Engine.Execution.Unit.Tests.Terraform;

internal static class TerraformTestData
{
    public static RunContext NewContext() => new("orders", Guid.NewGuid(), Guid.NewGuid());

    public static RunInput Input(RunInputProvider provider = RunInputProvider.Terraform, string key = "storage",
        IReadOnlyDictionary<string, string>? parameters = null, string templateName = "Storage Account",
        RunInputSource? source = null) =>
        new(key, templateName, "azure-storage-account", provider,
            source ?? new RunInputSource(new Uri("https://example.com/storage.git"), string.Empty, null),
            parameters ?? new Dictionary<string, string>());

    public static RunInput PlanInput() =>
        Input(parameters: new Dictionary<string, string> { ["name"] = "orders" });

    public static TerraformValidationResult.ValidResult ValidResult() =>
        TerraformValidationResult.Valid(new TerraformConfig
        {
            RequiredProviders = new Dictionary<string, TerraformConfig.RequiredProvider>
            {
                ["azurerm"] = new() { Source = "hashicorp/azurerm", VersionConstraints = ["~> 4.0"] }
            }
        }, "/modules/storage");

    public static Dictionary<RunInput, TerraformValidationResult.ValidResult> ValidatedPlans() =>
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
