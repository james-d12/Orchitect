namespace Orchitect.Engine.Execution.Provisioner.Terraform.Models;

public sealed record TerraformProvider(string Name, string Source, string Version);