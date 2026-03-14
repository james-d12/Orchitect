namespace Orchitect.Infrastructure.Engine.Terraform.Models;

public sealed record TerraformProjectBuilderResult(string StateDirectory, string PlanDirectory);