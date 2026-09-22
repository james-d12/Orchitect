namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public enum TerraformPlanResultExitCode
{
    NoChanges = 0,
    Errored = 1,
    ChangesNeeded = 2
}