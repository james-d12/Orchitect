namespace Orchitect.Engine.Execution.Provisioner.Terraform.Models;

public sealed record TerraformProjectBuilderResult(
    string WorkingDirectory,
    string PlanDirectory,
    string? BackendConfigFile);
