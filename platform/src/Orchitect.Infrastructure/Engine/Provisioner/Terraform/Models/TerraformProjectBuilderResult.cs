namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public sealed record TerraformProjectBuilderResult(string StateDirectory, string PlanDirectory);