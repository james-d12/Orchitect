using Orchitect.Domain.Engine.ResourceTemplate;
using Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform;

public sealed class TerraformProvisioner : IProvisioner
{
    public ResourceTemplateProvider Provider => ResourceTemplateProvider.Terraform;

    private readonly ITerraformDriver _terraformDriver;

    public TerraformProvisioner(ITerraformDriver terraformDriver)
    {
        _terraformDriver = terraformDriver;
    }

    public async Task ProvisionAsync(List<ProvisionInput> inputs, ProvisionContext context, CancellationToken cancellationToken = default)
    {
        var terraformProvisionInputs = inputs
            .Where(p => p.Template.Provider == ResourceTemplateProvider.Terraform)
            .ToList();

        if (terraformProvisionInputs.Count > 0)
        {
            var terraformPlanInputs = terraformProvisionInputs
                .Select(tp => new TerraformPlanInput(tp.Template, tp.Inputs, tp.Key))
                .ToList();
            TerraformPlanResult planResult = await _terraformDriver.PlanAsync(terraformPlanInputs, context);
            await _terraformDriver.ApplyAsync(planResult);
        }
    }

    public async Task DeleteAsync(List<ProvisionInput> inputs, ProvisionContext context, CancellationToken cancellationToken = default)
    {
        var terraformProvisionInputs = inputs
            .Where(p => p.Template.Provider == ResourceTemplateProvider.Terraform)
            .ToList();

        if (terraformProvisionInputs.Count > 0)
        {
            var terraformPlanInputs = terraformProvisionInputs
                .Select(tp => new TerraformPlanInput(tp.Template, tp.Inputs, tp.Key))
                .ToList();
            TerraformPlanResult planResult =
                await _terraformDriver.PlanAsync(terraformPlanInputs, context, destroy: true);
            await _terraformDriver.DestroyAsync(planResult);
        }
    }
}