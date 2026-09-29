using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Engine.Execution.Provisioner;

public sealed record ProvisionerInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);