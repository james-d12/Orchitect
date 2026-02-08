using Conductor.Inventory.Domain.Git;

namespace Conductor.Inventory.Infrastructure.AzureDevOps.Models;

public sealed record AzureDevOpsRepository : Repository
{
    public required bool IsDisabled { get; init; }
    public required bool IsInMaintenance { get; init; }
}