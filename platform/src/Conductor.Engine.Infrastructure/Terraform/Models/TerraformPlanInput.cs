using Conductor.Engine.Domain.ResourceTemplate;

namespace Conductor.Engine.Infrastructure.Terraform.Models;

public sealed record TerraformPlanInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);