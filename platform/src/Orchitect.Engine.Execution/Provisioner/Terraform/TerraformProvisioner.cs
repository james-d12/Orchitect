using Orchitect.Engine.Contracts.Runner.Api;
using Orchitect.Engine.Execution.Provisioner.Terraform.Models;

namespace Orchitect.Engine.Execution.Provisioner.Terraform;

public sealed class TerraformProvisioner : IProvisioner
{
    public RunInputProvider Provider => RunInputProvider.Terraform;

    private readonly ITerraformDriver _terraformDriver;

    public TerraformProvisioner(ITerraformDriver terraformDriver)
    {
        _terraformDriver = terraformDriver;
    }

    public async Task ProvisionAsync(List<RunInput> inputs, RunContext context, CancellationToken cancellationToken = default)
    {
        var terraformPlanInputs = inputs
            .Where(p => p.Provider == RunInputProvider.Terraform)
            .ToList();

        if (terraformPlanInputs.Count > 0)
        {
            TerraformPlanResult planResult = await _terraformDriver.PlanAsync(terraformPlanInputs, context,
                cancellationToken: cancellationToken);
            await _terraformDriver.ApplyAsync(planResult, cancellationToken);
        }
    }

    public async Task DeleteAsync(List<RunInput> inputs, RunContext context, CancellationToken cancellationToken = default)
    {
        var terraformPlanInputs = inputs
            .Where(p => p.Provider == RunInputProvider.Terraform)
            .ToList();

        if (terraformPlanInputs.Count > 0)
        {
            TerraformPlanResult planResult =
                await _terraformDriver.PlanAsync(terraformPlanInputs, context, destroy: true, cancellationToken);
            await _terraformDriver.DestroyAsync(planResult, cancellationToken);
        }
    }
}
