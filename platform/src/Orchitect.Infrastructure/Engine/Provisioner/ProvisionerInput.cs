using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Infrastructure.Engine.Provisioner;

public sealed record ProvisionerInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);