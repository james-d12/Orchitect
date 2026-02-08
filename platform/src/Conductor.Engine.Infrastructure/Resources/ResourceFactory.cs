using Conductor.Engine.Domain.ResourceTemplate;
using Conductor.Engine.Infrastructure.Terraform;
using Conductor.Engine.Infrastructure.Terraform.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Conductor.Engine.Infrastructure.Resources;

public interface IResourceFactory
{
    Task ProvisionAsync(List<ProvisionInput> provisionInputs, string folderName);
    Task DeleteAsync(List<ProvisionInput> provisionInputs, string folderName);
}

public sealed record ProvisionInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);

public sealed class ResourceFactory : IResourceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ResourceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ProvisionAsync(List<ProvisionInput> provisionInputs, string folderName)
    {
        var terraformProvisionInputs = provisionInputs
            .Where(p => p.Template.Provider == ResourceTemplateProvider.Terraform)
            .ToList();

        if (terraformProvisionInputs.Count > 0)
        {
            var terraformDriver = _serviceProvider.GetRequiredService<ITerraformDriver>();
            var terraformPlanInputs = terraformProvisionInputs
                .Select(tp => new TerraformPlanInput(tp.Template, tp.Inputs, tp.Key))
                .ToList();
            TerraformPlanResult planResult = await terraformDriver.PlanAsync(terraformPlanInputs, folderName);
            await terraformDriver.ApplyAsync(planResult);
        }
    }

    public async Task DeleteAsync(List<ProvisionInput> provisionInputs, string folderName)
    {
        var terraformProvisionInputs = provisionInputs
            .Where(p => p.Template.Provider == ResourceTemplateProvider.Terraform)
            .ToList();

        if (terraformProvisionInputs.Count > 0)
        {
            var terraformDriver = _serviceProvider.GetRequiredService<ITerraformDriver>();
            var terraformPlanInputs = terraformProvisionInputs
                .Select(tp => new TerraformPlanInput(tp.Template, tp.Inputs, tp.Key))
                .ToList();
            TerraformPlanResult planResult =
                await terraformDriver.PlanAsync(terraformPlanInputs, folderName, destroy: true);
            await terraformDriver.DestroyAsync(planResult);
        }
    }
}