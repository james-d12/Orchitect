namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public enum TerraformPlanResultState
{
    PreValidationFailed,
    InitFailed,
    ValidateFailed,
    PlanFailed,
    NoChanges,
    Success
}