using System.Diagnostics.CodeAnalysis;
using Orchitect.Domain.Inventory.SourceControl;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

[ExcludeFromCodeCoverage]
public sealed record AzureDevOpsRepository : Repository
{
    public required bool IsDisabled { get; init; }
    public required bool IsInMaintenance { get; init; }
}