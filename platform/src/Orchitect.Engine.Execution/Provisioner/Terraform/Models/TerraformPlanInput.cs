using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Engine.Execution.Provisioner.Terraform.Models;

public sealed record TerraformPlanInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);