namespace Orchitect.Infrastructure.Engine.Terraform.Models;

public sealed record TerraformProvider(string Name, string Source, string Version);