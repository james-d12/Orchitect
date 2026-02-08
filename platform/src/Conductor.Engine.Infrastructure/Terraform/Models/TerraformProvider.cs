namespace Conductor.Engine.Infrastructure.Terraform.Models;

public sealed record TerraformProvider(string Name, string Source, string Version);