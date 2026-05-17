using Orchitect.Domain.Engine.ResourceTemplate;

namespace Orchitect.Infrastructure.Engine.Shared;

public sealed record ProvisionerInput(ResourceTemplate Template, Dictionary<string, string> Inputs, string Key);