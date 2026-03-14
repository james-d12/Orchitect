using Orchitect.Domain.Inventory.Git;

namespace Orchitect.Infrastructure.Inventory.AzureDevOps.Models;

public sealed record AzureDevOpsRepository : Repository
{
    public required bool IsDisabled { get; init; }
    public required bool IsInMaintenance { get; init; }
}