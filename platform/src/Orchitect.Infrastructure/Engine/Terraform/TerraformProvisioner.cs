using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Shared;
using Orchitect.Infrastructure.Engine.Terraform.Models;

namespace Orchitect.Infrastructure.Engine.Terraform;

public sealed class TerraformProvisioner : IProvisioner
{
    public ResourceTemplateProvider Provider => ResourceTemplateProvider.Terraform;

    private readonly ITerraformDriver _terraformDriver;

    public TerraformProvisioner(ITerraformDriver terraformDriver)
    {
        _terraformDriver = terraformDriver;
    }

    public async Task ProvisionAsync(List<ProvisionInput> provisionInputs, string folderName, CancellationToken cancellationToken = default)
    {
        var terraformProvisionInputs = provisionInputs
            .Where(p => p.Template.Provider == ResourceTemplateProvider.Terraform)
            .ToList();

        if (terraformProvisionInputs.Count > 0)
        {
            var terraformPlanInputs = terraformProvisionInputs
                .Select(tp => new TerraformPlanInput(tp.Template, tp.Inputs, tp.Key))
                .ToList();
            TerraformPlanResult planResult = await _terraformDriver.PlanAsync(terraformPlanInputs, folderName);
            await _terraformDriver.ApplyAsync(planResult);
        }
    }

    public async Task DeleteAsync(List<ProvisionInput> provisionInputs, string folderName, CancellationToken cancellationToken = default)
    {
        var terraformProvisionInputs = provisionInputs
            .Where(p => p.Template.Provider == ResourceTemplateProvider.Terraform)
            .ToList();

        if (terraformProvisionInputs.Count > 0)
        {
            var terraformPlanInputs = terraformProvisionInputs
                .Select(tp => new TerraformPlanInput(tp.Template, tp.Inputs, tp.Key))
                .ToList();
            TerraformPlanResult planResult =
                await _terraformDriver.PlanAsync(terraformPlanInputs, folderName, destroy: true);
            await _terraformDriver.DestroyAsync(planResult);
        }
    }
}