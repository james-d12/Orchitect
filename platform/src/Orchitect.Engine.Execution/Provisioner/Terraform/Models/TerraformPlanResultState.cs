namespace Orchitect.Engine.Execution.Provisioner.Terraform.Models;

public enum TerraformPlanResultState
{
    PreValidationFailed,
    InitFailed,
    ValidateFailed,
    PlanFailed,
    NoChanges,
    Success
}