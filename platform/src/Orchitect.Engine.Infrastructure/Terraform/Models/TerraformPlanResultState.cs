namespace Orchitect.Engine.Infrastructure.Terraform.Models;

public enum TerraformPlanResultState
{
    PreValidationFailed,
    InitFailed,
    ValidateFailed,
    PlanFailed,
    NoChanges,
    Success
}