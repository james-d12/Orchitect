using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Infrastructure.Engine.Provisioner.Terraform.Models;

public sealed record TerraformPlanInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);