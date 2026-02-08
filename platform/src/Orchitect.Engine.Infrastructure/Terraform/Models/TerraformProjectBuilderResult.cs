namespace Orchitect.Engine.Infrastructure.Terraform.Models;

public sealed record TerraformProjectBuilderResult(string StateDirectory, string PlanDirectory);