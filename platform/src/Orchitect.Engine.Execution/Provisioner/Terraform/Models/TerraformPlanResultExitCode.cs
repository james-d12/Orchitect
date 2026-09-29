namespace Orchitect.Engine.Execution.Provisioner.Terraform.Models;

public enum TerraformPlanResultExitCode
{
    NoChanges = 0,
    Errored = 1,
    ChangesNeeded = 2
}