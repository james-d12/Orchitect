using Conductor.Inventory.Infrastructure.Shared;

namespace Conductor.Inventory.Infrastructure.Tests.Shared.Validation;

internal sealed class ValidationBuilderTestSettings : Settings
{
    public string TestProperty { get; init; } = string.Empty;
    public List<string> TestList { get; set; } = [];
}