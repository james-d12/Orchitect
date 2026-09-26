namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public sealed record TerraformRenderedModules(string MainTfJson, string TfVarsJson);
