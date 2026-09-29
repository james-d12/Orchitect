namespace Orchitect.Engine.Execution.Provisioner.Terraform.Models;

public sealed record TerraformRenderedModules(string MainTfJson, string TfVarsJson);
