using Orchitect.Inventory.Domain.Git;

namespace Orchitect.Inventory.Infrastructure.AzureDevOps.Models;

public sealed record AzureDevOpsRepository : Repository
{
    public required bool IsDisabled { get; init; }
    public required bool IsInMaintenance { get; init; }
}