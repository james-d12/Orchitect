namespace Orchitect.Infrastructure.Engine.Terraform.Models;

public enum TerraformPlanResultState
{
    PreValidationFailed,
    InitFailed,
    ValidateFailed,
    PlanFailed,
    NoChanges,
    Success
}