namespace Conductor.Engine.Infrastructure.Terraform.Models;

public sealed record TerraformProjectBuilderResult(string StateDirectory, string PlanDirectory);