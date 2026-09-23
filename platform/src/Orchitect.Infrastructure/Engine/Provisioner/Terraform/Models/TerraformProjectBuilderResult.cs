namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public sealed record TerraformProjectBuilderResult(
    string WorkingDirectory,
    string PlanDirectory,
    IReadOnlyDictionary<string, string> BackendConfig);
