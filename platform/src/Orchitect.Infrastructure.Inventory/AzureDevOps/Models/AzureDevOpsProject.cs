using System.Diagnostics.CodeAnalysis;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

[ExcludeFromCodeCoverage]
public sealed record AzureDevOpsProject
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Uri Url { get; init; }
}