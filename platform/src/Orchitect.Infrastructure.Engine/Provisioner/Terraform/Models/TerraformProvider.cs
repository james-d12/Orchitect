namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public sealed record TerraformProvider(string Name, string Source, string Version);