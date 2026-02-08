using Orchitect.Engine.Domain.ResourceTemplate;

namespace Orchitect.Engine.Infrastructure.Terraform.Models;

public sealed record TerraformPlanInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);